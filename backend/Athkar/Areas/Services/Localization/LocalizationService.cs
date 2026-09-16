using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Localization;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Localization.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Localization;

/// <summary>
/// Languages, and the interface copy that hangs off them.
///
/// The point of the whole slice is that adding Turkish is an afternoon in the
/// CMS rather than a release of three clients: a row here, a set of strings, and
/// the translations on the content itself.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly IRepository<AppLanguage> languages;
    private readonly IRepository<UiString> strings;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly ISecurityManager securityManager;

    public LocalizationService(
        IRepository<AppLanguage> languages,
        IRepository<UiString> strings,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ISecurityManager securityManager)
    {
        this.languages = languages;
        this.strings = strings;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<List<LanguageOutput>>> EnabledLanguages() =>
        new(await Describe(languages.Query().Where(l => l.IsEnabled)));

    public async Task<BaseResponse<List<LanguageOutput>>> AllLanguages() =>
        new(await Describe(languages.Query()));

    public async Task<BaseResponse<LanguageOutput>> CreateLanguage(LanguageInput input)
    {
        var code = input.Code.Trim().ToLowerInvariant();

        if (await languages.AnyAsync(l => l.Code == code))
            return BaseResponse<LanguageOutput>.Fail(ErrorCode.LanguageAlreadyExists);

        var language = new AppLanguage
        {
            Code = code,
            NativeName = input.NativeName.Trim(),
            EnglishName = input.EnglishName.Trim(),
            IsRtl = input.IsRtl,
            IsEnabled = input.IsEnabled,
            SortOrder = input.SortOrder,
            // The first language to exist has to be the default, or nothing has
            // a fallback and every resolution ends in the hardcoded constant.
            IsDefault = !await languages.AnyAsync(l => l.IsDefault),
            CreatedBy = securityManager.UserId,
        };

        await languages.AddAsync(language);
        await unitOfWork.SaveAsync();

        var output = new LanguageOutput(language);
        await auditService.LogAsync(AuditActions.LanguageCreate, nameof(AppLanguage),
            language.Id, null, output);

        return new BaseResponse<LanguageOutput>(output);
    }

    public async Task<BaseResponse<LanguageOutput>> UpdateLanguage(int id, LanguageInput input)
    {
        var language = await languages.GetByIdAsync(id);
        if (language is null) return BaseResponse<LanguageOutput>.Fail(ErrorCode.LanguageNotFound);

        var code = input.Code.Trim().ToLowerInvariant();
        if (code != language.Code && await languages.AnyAsync(l => l.Code == code))
            return BaseResponse<LanguageOutput>.Fail(ErrorCode.LanguageAlreadyExists);

        // The default is where every fallback lands, so it cannot be switched
        // off — and Arabic cannot be touched at all, because the corpus is
        // written in it and a "disabled" source language would leave adhkar with
        // no text rather than no translation.
        if (!input.IsEnabled)
        {
            if (language.IsDefault)
                return BaseResponse<LanguageOutput>.Fail(ErrorCode.DefaultLanguageImmutable);

            if (language.Code == ContentRules.SourceLanguage)
                return BaseResponse<LanguageOutput>.Fail(ErrorCode.SourceLanguageImmutable);
        }

        var before = new LanguageOutput(language);

        language.Code = code;
        language.NativeName = input.NativeName.Trim();
        language.EnglishName = input.EnglishName.Trim();
        language.IsRtl = input.IsRtl;
        language.IsEnabled = input.IsEnabled;
        language.SortOrder = input.SortOrder;
        language.Version++;
        language.ModifiedBy = securityManager.UserId;
        languages.Update(language);

        await unitOfWork.SaveAsync();

        var output = new LanguageOutput(language);
        await auditService.LogAsync(AuditActions.LanguageUpdate, nameof(AppLanguage),
            language.Id, before, output);

        return new BaseResponse<LanguageOutput>(output);
    }

    public async Task<BaseResponse> DeleteLanguage(int id)
    {
        var language = await languages.GetByIdAsync(id);
        if (language is null) return BaseResponse.Fail(ErrorCode.LanguageNotFound);

        if (language.IsDefault) return BaseResponse.Fail(ErrorCode.DefaultLanguageImmutable);
        if (language.Code == ContentRules.SourceLanguage)
            return BaseResponse.Fail(ErrorCode.SourceLanguageImmutable);

        var overlay = await strings.Where(s => s.LanguageId == language.Id).ToListAsync();
        strings.SoftDeleteRange(overlay);
        languages.SoftDelete(language);

        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.LanguageDelete, nameof(AppLanguage),
            id, new LanguageOutput(language), null);

        return new BaseResponse();
    }

    public async Task<BaseResponse<LanguageOutput>> SetDefaultLanguage(int id)
    {
        var language = await languages.GetByIdAsync(id);
        if (language is null) return BaseResponse<LanguageOutput>.Fail(ErrorCode.LanguageNotFound);

        if (!language.IsEnabled)
            return BaseResponse<LanguageOutput>.Fail(ErrorCode.ValidationError,
                "A disabled language cannot be the default.");

        var current = await languages.Where(l => l.IsDefault).ToListAsync();
        foreach (var other in current.Where(l => l.Id != language.Id))
        {
            other.IsDefault = false;
            languages.Update(other);
        }

        language.IsDefault = true;
        language.ModifiedBy = securityManager.UserId;
        languages.Update(language);

        await unitOfWork.SaveAsync();

        return new BaseResponse<LanguageOutput>(new LanguageOutput(language));
    }

    public async Task<BaseResponse<UiStringsOutput>> Strings(string languageCode)
    {
        var code = languageCode.Trim().ToLowerInvariant();
        var language = await languages.FirstOrDefaultAsync(l => l.Code == code);

        if (language is null) return BaseResponse<UiStringsOutput>.Fail(ErrorCode.LanguageNotFound);

        return new BaseResponse<UiStringsOutput>(new UiStringsOutput
        {
            LanguageCode = language.Code,
            Version = language.Version,
            Strings = await strings.Where(s => s.LanguageId == language.Id)
                .ToDictionaryAsync(s => s.Key, s => s.Value),
        });
    }

    public async Task<BaseResponse<UiStringsOutput>> ReplaceStrings(string languageCode, UiStringsInput input)
    {
        var code = languageCode.Trim().ToLowerInvariant();
        var language = await languages.FirstOrDefaultAsync(l => l.Code == code);

        if (language is null) return BaseResponse<UiStringsOutput>.Fail(ErrorCode.LanguageNotFound);

        var wanted = input.Strings
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .ToDictionary(kv => kv.Key.Trim(), kv => kv.Value ?? string.Empty);

        var existing = await strings.Where(s => s.LanguageId == language.Id).ToListAsync();

        foreach (var row in existing)
        {
            if (wanted.TryGetValue(row.Key, out var value))
            {
                if (row.Value != value)
                {
                    row.Value = value;
                    strings.Update(row);
                }
                wanted.Remove(row.Key);
            }
            else
            {
                // Removed, not blanked. An empty override would have the app
                // render an empty button; removing it returns the reader to the
                // copy compiled into the build.
                strings.SoftDelete(row);
            }
        }

        foreach (var (key, value) in wanted)
            await strings.AddAsync(new UiString { LanguageId = language.Id, Key = key, Value = value });

        language.Version++;
        languages.Update(language);

        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.UiStringsImport, nameof(UiString), language.Id,
            null, new { language.Code, Count = input.Strings.Count });

        return await Strings(code);
    }

    private async Task<List<LanguageOutput>> Describe(IQueryable<AppLanguage> query)
    {
        var rows = await query
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Id)
            .Select(l => new
            {
                Language = l,
                StringCount = strings.Query(false).Count(s => s.LanguageId == l.Id),
            })
            .ToListAsync();

        return
        [
            .. rows.Select(row =>
            {
                var output = new LanguageOutput(row.Language);
                output.StringCount = row.StringCount;
                return output;
            }),
        ];
    }
}
