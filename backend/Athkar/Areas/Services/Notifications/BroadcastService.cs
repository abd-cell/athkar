using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Notifications;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Notifications.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Extensions;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Notifications;

public class BroadcastService : IBroadcastService
{
    private readonly IRepository<Broadcast> broadcasts;
    private readonly IRepository<BroadcastTranslation> translations;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly ISecurityManager securityManager;

    public BroadcastService(
        IRepository<Broadcast> broadcasts,
        IRepository<BroadcastTranslation> translations,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ISecurityManager securityManager)
    {
        this.broadcasts = broadcasts;
        this.translations = translations;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<PageOutput<BroadcastOutput>>> List(PageInput input)
    {
        var query = broadcasts.Query().Include(b => b.Translations).AsQueryable();

        if (!string.IsNullOrWhiteSpace(input.Search))
        {
            var term = input.Search.Trim();
            query = query.Where(b => b.Translations.Any(t => !t.IsDeleted && t.Title.Contains(term)));
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(b => b.CreationDate)
            .Paginate(input)
            .ToListAsync();

        return new BaseResponse<PageOutput<BroadcastOutput>>(new PageOutput<BroadcastOutput>
        {
            TotalRows = total,
            Data = [.. rows.Select(b => new BroadcastOutput(b))],
        });
    }

    public async Task<BaseResponse<BroadcastOutput>> Get(int id)
    {
        var broadcast = await Load(id);
        return broadcast is null
            ? BaseResponse<BroadcastOutput>.Fail(ErrorCode.BroadcastNotFound)
            : new BaseResponse<BroadcastOutput>(new BroadcastOutput(broadcast));
    }

    public async Task<BaseResponse<BroadcastOutput>> Create(BroadcastInput input)
    {
        if (Validate(input) is { } failure)
            return BaseResponse<BroadcastOutput>.Fail(failure);

        var broadcast = new Broadcast
        {
            Status = BroadcastStatus.Draft,
            CreatedBy = securityManager.UserId,
        };
        Apply(broadcast, input);

        foreach (var translation in Distinct(input.Translations))
            broadcast.Translations.Add(Build(translation));

        await broadcasts.AddAsync(broadcast);
        await unitOfWork.SaveAsync();

        var output = new BroadcastOutput(broadcast);
        await auditService.LogAsync(AuditActions.BroadcastCreate, nameof(Broadcast),
            broadcast.Id, null, output);

        return new BaseResponse<BroadcastOutput>(output);
    }

    public async Task<BaseResponse<BroadcastOutput>> Update(int id, BroadcastInput input)
    {
        var broadcast = await Load(id);
        if (broadcast is null) return BaseResponse<BroadcastOutput>.Fail(ErrorCode.BroadcastNotFound);

        // Once it has started going out there is no editing it: some readers
        // already have the old wording on their lock screen, and changing the
        // row would leave the CMS describing a message that was never sent.
        if (broadcast.Status is not (BroadcastStatus.Draft or BroadcastStatus.Scheduled or BroadcastStatus.Cancelled))
            return BaseResponse<BroadcastOutput>.Fail(ErrorCode.BroadcastAlreadySent);

        if (Validate(input) is { } failure)
            return BaseResponse<BroadcastOutput>.Fail(failure);

        var before = new BroadcastOutput(broadcast);

        Apply(broadcast, input);
        broadcast.ModifiedBy = securityManager.UserId;
        broadcasts.Update(broadcast);

        SyncTranslations(broadcast, input.Translations);

        await unitOfWork.SaveAsync();

        var output = new BroadcastOutput(broadcast);
        await auditService.LogAsync(AuditActions.BroadcastUpdate, nameof(Broadcast),
            broadcast.Id, before, output);

        return new BaseResponse<BroadcastOutput>(output);
    }

    public async Task<BaseResponse<BroadcastOutput>> Send(int id)
    {
        var broadcast = await Load(id);
        if (broadcast is null) return BaseResponse<BroadcastOutput>.Fail(ErrorCode.BroadcastNotFound);

        if (broadcast.Status is BroadcastStatus.Sending or BroadcastStatus.Sent)
            return BaseResponse<BroadcastOutput>.Fail(ErrorCode.BroadcastAlreadySent);

        if (broadcast.ScheduledAtUtc is { } when && when <= DateTime.UtcNow.AddSeconds(-30))
            return BaseResponse<BroadcastOutput>.Fail(ErrorCode.ScheduleMustBeFuture);

        if (!broadcast.Translations.Any(t => !t.IsDeleted))
            return BaseResponse<BroadcastOutput>.Fail(ErrorCode.ValidationError,
                "The message has no wording.");

        broadcast.Status = BroadcastStatus.Scheduled;
        broadcast.ModifiedBy = securityManager.UserId;
        broadcasts.Update(broadcast);

        await unitOfWork.SaveAsync();

        var output = new BroadcastOutput(broadcast);
        await auditService.LogAsync(AuditActions.BroadcastSend, nameof(Broadcast),
            broadcast.Id, null, output);

        return new BaseResponse<BroadcastOutput>(output);
    }

    public async Task<BaseResponse<BroadcastOutput>> Cancel(int id)
    {
        var broadcast = await Load(id);
        if (broadcast is null) return BaseResponse<BroadcastOutput>.Fail(ErrorCode.BroadcastNotFound);

        if (broadcast.Status != BroadcastStatus.Scheduled)
            return BaseResponse<BroadcastOutput>.Fail(ErrorCode.BroadcastAlreadySent);

        broadcast.Status = BroadcastStatus.Cancelled;
        broadcast.ModifiedBy = securityManager.UserId;
        broadcasts.Update(broadcast);

        await unitOfWork.SaveAsync();

        var output = new BroadcastOutput(broadcast);
        await auditService.LogAsync(AuditActions.BroadcastCancel, nameof(Broadcast),
            broadcast.Id, null, output);

        return new BaseResponse<BroadcastOutput>(output);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var broadcast = await Load(id);
        if (broadcast is null) return BaseResponse.Fail(ErrorCode.BroadcastNotFound);

        if (broadcast.Status is BroadcastStatus.Sending)
            return BaseResponse.Fail(ErrorCode.BroadcastAlreadySent);

        translations.SoftDeleteRange(broadcast.Translations.Where(t => !t.IsDeleted));
        broadcasts.SoftDelete(broadcast);

        await unitOfWork.SaveAsync();
        return new BaseResponse();
    }

    private static ErrorCode? Validate(BroadcastInput input)
    {
        if (input.ScheduledAtUtc is { } when && when <= DateTime.UtcNow.AddSeconds(-30))
            return ErrorCode.ScheduleMustBeFuture;

        return input.Audience switch
        {
            Audience.Language when string.IsNullOrWhiteSpace(input.TargetLanguageCode) =>
                ErrorCode.ValidationError,
            Audience.Platform when input.TargetPlatform is null => ErrorCode.ValidationError,
            Audience.Device when string.IsNullOrWhiteSpace(input.TargetDeviceKey) =>
                ErrorCode.ValidationError,
            _ => null,
        };
    }

    private static void Apply(Broadcast broadcast, BroadcastInput input)
    {
        broadcast.Audience = input.Audience;
        broadcast.TargetLanguageCode = input.Audience == Audience.Language
            ? input.TargetLanguageCode?.Trim().ToLowerInvariant()
            : null;
        broadcast.TargetPlatform = input.Audience == Audience.Platform ? input.TargetPlatform : null;
        broadcast.TargetDeviceKey = input.Audience == Audience.Device
            ? input.TargetDeviceKey?.Trim()
            : null;
        broadcast.ScheduledAtUtc = input.ScheduledAtUtc;
        broadcast.Route = string.IsNullOrWhiteSpace(input.Route) ? null : input.Route.Trim();
    }

    private Task<Broadcast?> Load(int id) =>
        broadcasts.Query().Include(b => b.Translations).FirstOrDefaultAsync(b => b.Id == id);

    private static IEnumerable<TranslationInput> Distinct(IEnumerable<TranslationInput> input) =>
        input
            .Where(t => !string.IsNullOrWhiteSpace(t.LanguageCode) && !string.IsNullOrWhiteSpace(t.Title))
            .GroupBy(t => t.LanguageCode.Trim().ToLowerInvariant())
            .Select(g => g.Last());

    private static BroadcastTranslation Build(TranslationInput input) => new()
    {
        LanguageCode = input.LanguageCode.Trim().ToLowerInvariant(),
        Title = input.Title.Trim(),
        Body = (input.Body ?? string.Empty).Trim(),
    };

    private void SyncTranslations(Broadcast broadcast, List<TranslationInput> wanted)
    {
        var incoming = Distinct(wanted).ToDictionary(t => t.LanguageCode.Trim().ToLowerInvariant());

        foreach (var existing in broadcast.Translations.Where(t => !t.IsDeleted).ToList())
        {
            if (incoming.TryGetValue(existing.LanguageCode, out var update))
            {
                existing.Title = update.Title.Trim();
                existing.Body = (update.Body ?? string.Empty).Trim();
                translations.Update(existing);
                incoming.Remove(existing.LanguageCode);
            }
            else
            {
                translations.SoftDelete(existing);
            }
        }

        foreach (var (_, translation) in incoming)
        {
            var row = Build(translation);
            row.BroadcastId = broadcast.Id;
            broadcast.Translations.Add(row);
        }
    }
}
