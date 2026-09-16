using Athkar.Areas.Services.Localization.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Localization;

[ScopedInjectable]
public interface ILocalizationService
{
    /// <summary>The languages the app may offer. Anonymous — the picker needs it before anything else.</summary>
    Task<BaseResponse<List<LanguageOutput>>> EnabledLanguages();

    /// <summary>Every language, enabled or not. Admin.</summary>
    Task<BaseResponse<List<LanguageOutput>>> AllLanguages();

    Task<BaseResponse<LanguageOutput>> CreateLanguage(LanguageInput input);
    Task<BaseResponse<LanguageOutput>> UpdateLanguage(int id, LanguageInput input);
    Task<BaseResponse> DeleteLanguage(int id);

    /// <summary>Makes one language the fallback for every other. Exactly one holds it.</summary>
    Task<BaseResponse<LanguageOutput>> SetDefaultLanguage(int id);

    /// <summary>The interface-copy overlay for one language.</summary>
    Task<BaseResponse<UiStringsOutput>> Strings(string languageCode);

    /// <summary>Replaces that overlay wholesale and bumps the language version.</summary>
    Task<BaseResponse<UiStringsOutput>> ReplaceStrings(string languageCode, UiStringsInput input);
}
