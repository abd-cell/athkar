using Athkar.Areas.Services.Configuration.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Configuration;

[ScopedInjectable]
public interface IAppConfigurationService
{
    /// <summary>The current settings. Never fails: an empty table yields the defaults.</summary>
    Task<BaseResponse<AppConfigurationOutput>> Get();

    /// <summary>Replaces the settings. Admin-only at the controller; audited here.</summary>
    Task<BaseResponse<AppConfigurationOutput>> Update(AppConfigurationInput input);

    /// <summary>
    /// Bumps the content version, so every app learns on its next launch that
    /// the catalogue has moved. Called by the content services after any change
    /// a reader would see — never by a controller.
    /// </summary>
    Task<int> BumpContentVersion();
}
