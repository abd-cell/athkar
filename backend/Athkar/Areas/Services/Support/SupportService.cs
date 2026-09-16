using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Support;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Localization;
using Athkar.Areas.Services.Support.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Support;

public class SupportService : ISupportService
{
    private readonly IRepository<FaqItem> faqItems;
    private readonly IRepository<FaqTranslation> faqTranslations;
    private readonly IRepository<Feedback> feedback;
    private readonly IRepository<Device> devices;
    private readonly IRepository<DeviceNotification> inbox;
    private readonly IRepository<Dhikr> adhkar;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly ILanguageResolver languages;
    private readonly IDeviceContext deviceContext;
    private readonly ISecurityManager securityManager;

    public SupportService(
        IRepository<FaqItem> faqItems,
        IRepository<FaqTranslation> faqTranslations,
        IRepository<Feedback> feedback,
        IRepository<Device> devices,
        IRepository<DeviceNotification> inbox,
        IRepository<Dhikr> adhkar,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ILanguageResolver languages,
        IDeviceContext deviceContext,
        ISecurityManager securityManager)
    {
        this.faqItems = faqItems;
        this.faqTranslations = faqTranslations;
        this.feedback = feedback;
        this.devices = devices;
        this.inbox = inbox;
        this.adhkar = adhkar;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.languages = languages;
        this.deviceContext = deviceContext;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<List<FaqOutput>>> Faq(string? languageCode)
    {
        var language = await languages.Resolve(languageCode);
        var fallback = await languages.Default();

        var rows = await faqItems.Query()
            .Where(f => f.IsPublished)
            .Include(f => f.Translations)
            .OrderBy(f => f.Category).ThenBy(f => f.SortOrder)
            .ToListAsync();

        var output = rows
            .Select(item =>
            {
                var live = item.Translations.Where(t => !t.IsDeleted).ToList();
                var wording = live.FirstOrDefault(t => t.LanguageCode == language)
                              ?? live.FirstOrDefault(t => t.LanguageCode == fallback);

                return wording is null ? null : new FaqOutput
                {
                    Id = item.Id,
                    Category = item.Category,
                    SortOrder = item.SortOrder,
                    Question = wording.Question,
                    Answer = wording.Answer,
                };
            })
            .OfType<FaqOutput>()
            .ToList();

        return new BaseResponse<List<FaqOutput>>(output);
    }

    public async Task<BaseResponse> SubmitFeedback(FeedbackInput input)
    {
        var key = deviceContext.RequireDeviceKey();
        var device = await devices.FirstOrDefaultAsync(d => d.DeviceKey == key);

        if (device is null) return BaseResponse.Fail(ErrorCode.DeviceNotFound);

        // An anonymous reader cannot be blocked by account, so the count of what
        // they already have open is the only brake there is on a device sending
        // the desk a thousand messages.
        var open = await feedback.CountAsync(f =>
            f.DeviceId == device.Id &&
            (f.Status == FeedbackStatus.New || f.Status == FeedbackStatus.InProgress));

        if (open >= ContentRules.MaxOpenFeedbackPerDevice)
            return BaseResponse.Fail(ErrorCode.TooManyOpenFeedback);

        await feedback.AddAsync(new Feedback
        {
            DeviceId = device.Id,
            Kind = input.Kind,
            Message = input.Message.Trim(),
            DhikrId = input.DhikrId,
            ContactEmail = string.IsNullOrWhiteSpace(input.ContactEmail)
                ? null
                : input.ContactEmail.Trim().ToLowerInvariant(),
            AppVersion = string.IsNullOrWhiteSpace(input.AppVersion) ? null : input.AppVersion.Trim(),
            LanguageCode = device.LanguageCode,
        });

        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }

    // ───────────────────────────── desk: help ─────────────────────────────

    public async Task<BaseResponse<PageOutput<AdminFaqOutput>>> ListFaq(PageInput input)
    {
        var query = faqItems.Query().Include(f => f.Translations).AsQueryable();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(f => f.Translations.Any(t => !t.IsDeleted && t.Question.Contains(term)));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderBy(f => f.Category).ThenBy(f => f.SortOrder)
            .Paginate(input)
            .ToListAsync();

        return new BaseResponse<PageOutput<AdminFaqOutput>>(new PageOutput<AdminFaqOutput>
        {
            TotalRows = total,
            Data = [.. rows.Select(Describe)],
        });
    }

    public async Task<BaseResponse<AdminFaqOutput>> CreateFaq(FaqInput input)
    {
        var item = new FaqItem
        {
            Category = input.Category,
            SortOrder = input.SortOrder,
            IsPublished = input.IsPublished,
            CreatedBy = securityManager.UserId,
        };

        foreach (var translation in Distinct(input.Translations))
            item.Translations.Add(Build(translation));

        await faqItems.AddAsync(item);
        await unitOfWork.SaveAsync();

        var output = Describe(item);
        await auditService.LogAsync(AuditActions.FaqCreate, nameof(FaqItem), item.Id, null, output);

        return new BaseResponse<AdminFaqOutput>(output);
    }

    public async Task<BaseResponse<AdminFaqOutput>> UpdateFaq(int id, FaqInput input)
    {
        var item = await faqItems.Query()
            .Include(f => f.Translations)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (item is null) return BaseResponse<AdminFaqOutput>.Fail(ErrorCode.FaqNotFound);

        var before = Describe(item);

        item.Category = input.Category;
        item.SortOrder = input.SortOrder;
        item.IsPublished = input.IsPublished;
        item.ModifiedBy = securityManager.UserId;
        faqItems.Update(item);

        var incoming = Distinct(input.Translations)
            .ToDictionary(t => t.LanguageCode.Trim().ToLowerInvariant());

        foreach (var existing in item.Translations.Where(t => !t.IsDeleted).ToList())
        {
            if (incoming.TryGetValue(existing.LanguageCode, out var update))
            {
                existing.Question = update.Title.Trim();
                existing.Answer = (update.Body ?? string.Empty).Trim();
                faqTranslations.Update(existing);
                incoming.Remove(existing.LanguageCode);
            }
            else
            {
                faqTranslations.SoftDelete(existing);
            }
        }

        foreach (var (_, translation) in incoming)
        {
            var row = Build(translation);
            row.FaqItemId = item.Id;
            item.Translations.Add(row);
        }

        await unitOfWork.SaveAsync();

        var output = Describe(item);
        await auditService.LogAsync(AuditActions.FaqUpdate, nameof(FaqItem), item.Id, before, output);

        return new BaseResponse<AdminFaqOutput>(output);
    }

    public async Task<BaseResponse> DeleteFaq(int id)
    {
        var item = await faqItems.Query()
            .Include(f => f.Translations)
            .FirstOrDefaultAsync(f => f.Id == id);

        if (item is null) return BaseResponse.Fail(ErrorCode.FaqNotFound);

        faqTranslations.SoftDeleteRange(item.Translations.Where(t => !t.IsDeleted));
        faqItems.SoftDelete(item);
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.FaqDelete, nameof(FaqItem), id, Describe(item), null);

        return new BaseResponse();
    }

    // ──────────────────────────── desk: inbox ────────────────────────────

    public async Task<BaseResponse<PageOutput<FeedbackOutput>>> ListFeedback(
        FeedbackStatus? status, PageInput input)
    {
        var query = feedback.Query();

        if (status is not null) query = query.Where(f => f.Status == status);

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(f => f.Message.Contains(term));
        }

        var total = await query.CountAsync();

        var rows = await query
            // Corrections first: a reader telling us a grading is wrong is the
            // most valuable message this desk receives, and it should not sit
            // behind a week of praise.
            .OrderBy(f => f.Kind == FeedbackKind.Correction ? 0 : 1)
            .ThenByDescending(f => f.CreationDate)
            .Paginate(input)
            .Select(f => new
            {
                Feedback = f,
                DhikrText = f.DhikrId == null
                    ? null
                    : adhkar.Query(false).Where(d => d.Id == f.DhikrId)
                        .Select(d => d.ArabicText).FirstOrDefault(),
                RepliedByName = f.RepliedByUser!.FullName,
            })
            .ToListAsync();

        return new BaseResponse<PageOutput<FeedbackOutput>>(new PageOutput<FeedbackOutput>
        {
            TotalRows = total,
            Data =
            [
                .. rows.Select(row => new FeedbackOutput
                {
                    Id = row.Feedback.Id,
                    Kind = row.Feedback.Kind,
                    Status = row.Feedback.Status,
                    Message = row.Feedback.Message,
                    DhikrId = row.Feedback.DhikrId,
                    DhikrText = row.DhikrText,
                    ContactEmail = row.Feedback.ContactEmail,
                    AppVersion = row.Feedback.AppVersion,
                    LanguageCode = row.Feedback.LanguageCode,
                    Reply = row.Feedback.Reply,
                    RepliedAt = row.Feedback.RepliedAt,
                    RepliedByName = row.RepliedByName,
                    CreatedAt = row.Feedback.CreationDate,
                }),
            ],
        });
    }

    public async Task<BaseResponse<FeedbackOutput>> Reply(int id, FeedbackReplyInput input)
    {
        var row = await feedback.GetByIdAsync(id);
        if (row is null) return BaseResponse<FeedbackOutput>.Fail(ErrorCode.FeedbackNotFound);

        row.Reply = input.Reply.Trim();
        row.RepliedAt = DateTime.UtcNow;
        row.RepliedBy = securityManager.UserId;
        row.Status = input.Status;
        feedback.Update(row);

        // Delivered as an inbox row rather than a push: the reader wrote in days
        // ago and a notification arriving out of the blue reads as an
        // advertisement. It is waiting for them the next time they look.
        await inbox.AddAsync(new DeviceNotification
        {
            DeviceId = row.DeviceId,
            Kind = NotificationKind.System,
            Title = "رد على رسالتك",
            Body = row.Reply,
            LanguageCode = row.LanguageCode,
        });

        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.FeedbackReply, nameof(Feedback), row.Id,
            null, new { row.Status, row.Reply });

        return new BaseResponse<FeedbackOutput>(new FeedbackOutput
        {
            Id = row.Id,
            Kind = row.Kind,
            Status = row.Status,
            Message = row.Message,
            DhikrId = row.DhikrId,
            ContactEmail = row.ContactEmail,
            AppVersion = row.AppVersion,
            LanguageCode = row.LanguageCode,
            Reply = row.Reply,
            RepliedAt = row.RepliedAt,
            CreatedAt = row.CreationDate,
        });
    }

    private static IEnumerable<TranslationInput> Distinct(IEnumerable<TranslationInput> input) =>
        input
            .Where(t => !string.IsNullOrWhiteSpace(t.LanguageCode) && !string.IsNullOrWhiteSpace(t.Title))
            .GroupBy(t => t.LanguageCode.Trim().ToLowerInvariant())
            .Select(g => g.Last());

    private static FaqTranslation Build(TranslationInput input) => new()
    {
        LanguageCode = input.LanguageCode.Trim().ToLowerInvariant(),
        Question = input.Title.Trim(),
        Answer = (input.Body ?? string.Empty).Trim(),
    };

    private static AdminFaqOutput Describe(FaqItem item)
    {
        var live = item.Translations.Where(t => !t.IsDeleted).ToList();

        return new AdminFaqOutput
        {
            Id = item.Id,
            Category = item.Category,
            SortOrder = item.SortOrder,
            IsPublished = item.IsPublished,
            TranslatedLanguages = [.. live.Select(t => t.LanguageCode).Order()],
            Translations =
            [
                .. live.Select(t => new TranslationInput
                {
                    LanguageCode = t.LanguageCode,
                    Title = t.Question,
                    Body = t.Answer,
                }),
            ],
        };
    }
}
