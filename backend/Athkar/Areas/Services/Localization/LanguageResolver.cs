using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Localization;
using Athkar.DataAccess.Repositories;
using Athkar.Shareds.Constants;

namespace Athkar.Areas.Services.Localization;

public class LanguageResolver : ILanguageResolver
{
    private readonly IRepository<AppLanguage> languages;

    public LanguageResolver(IRepository<AppLanguage> languages) => this.languages = languages;

    public async Task<string> Resolve(string? requested)
    {
        var code = Simplify(requested);

        if (code.Length > 0 && await languages.AnyAsync(l => l.Code == code && l.IsEnabled))
            return code;

        return await Default();
    }

    public async Task<string> Default()
    {
        var code = await languages.Query()
            .Where(l => l.IsEnabled && l.IsDefault)
            .Select(l => l.Code)
            .FirstOrDefaultAsync();

        // An empty or misconfigured language table must not leave a caller with
        // no language at all, and the corpus is written in Arabic regardless of
        // what any row says.
        return code ?? ContentRules.SourceLanguage;
    }

    /// <summary>
    /// "ar-SA" → "ar", "EN" → "en". Regional variants are collapsed because the
    /// content is translated per language and not per country — an app sending
    /// its full locale should get Arabic, not the default.
    /// </summary>
    private static string Simplify(string? requested)
    {
        var raw = requested?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var separator = raw.IndexOfAny(['-', '_']);
        return separator > 0 ? raw[..separator] : raw;
    }
}
