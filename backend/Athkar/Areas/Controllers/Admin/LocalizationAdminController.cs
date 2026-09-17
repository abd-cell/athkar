using Microsoft.AspNetCore.Mvc;
using Athkar.Areas.Services.Localization;
using Athkar.Areas.Services.Localization.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Controllers.Admin;

[AppAuthorize(Roles.Admin, Roles.SuperAdmin)]
[Route("api/v1/admin/languages")]
public class LocalizationAdminController : BaseApiController
{
    private readonly ILocalizationService service;

    public LocalizationAdminController(ILocalizationService service) => this.service = service;

    // Every editor-open content screen (Categories, Adhkar, Radio, FAQ, the
    // widget catalogue) reads the language list to build its tab strip, so it
    // is opened to Editors the same way the strings endpoints below are — the
    // mutating actions on this controller stay Admin-and-above.
    [AppAuthorize(Roles.Editor, Roles.Admin, Roles.SuperAdmin)]
    [HttpGet]
    public Task<BaseResponse<List<LanguageOutput>>> List() => service.AllLanguages();

    [HttpPost]
    public Task<BaseResponse<LanguageOutput>> Create([FromBody] LanguageInput input) =>
        service.CreateLanguage(input);

    [HttpPut("{id:int}")]
    public Task<BaseResponse<LanguageOutput>> Update(int id, [FromBody] LanguageInput input) =>
        service.UpdateLanguage(id, input);

    [HttpDelete("{id:int}")]
    public Task<BaseResponse> Delete(int id) => service.DeleteLanguage(id);

    [HttpPost("{id:int}/default")]
    public Task<BaseResponse<LanguageOutput>> SetDefault(int id) => service.SetDefaultLanguage(id);

    [AppAuthorize(Roles.Editor, Roles.Admin, Roles.SuperAdmin)]
    [HttpGet("{code}/strings")]
    public Task<BaseResponse<UiStringsOutput>> Strings(string code) => service.Strings(code);

    [AppAuthorize(Roles.Editor, Roles.Admin, Roles.SuperAdmin)]
    [HttpPut("{code}/strings")]
    public Task<BaseResponse<UiStringsOutput>> ReplaceStrings(string code, [FromBody] UiStringsInput input) =>
        service.ReplaceStrings(code, input);
}
