using Athkar.Areas.Domain.Radio;
using Athkar.Areas.Services.Content.Models;
using Athkar.Areas.Services.Radio;
using Athkar.Areas.Services.Radio.Models;
using Athkar.Shareds.Models;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// The station list, and the one rule that is peculiar to it: a stream must be
/// https.
///
/// It is checked on the server because the failure it prevents is invisible
/// from the console — a cleartext stream is refused by the phone's own network
/// policy, so an editor who saved one would see a perfectly good-looking row
/// and a reader would hear nothing. The content-version rule is tested here too
/// for the usual reason: a station that does not bump it is a station that
/// never reaches a phone.
/// </summary>
public class RadioAdminServiceTests
{
    private readonly InMemoryRepository<RadioStation> stations = new();
    private readonly InMemoryRepository<RadioStationTranslation> translations = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeAuditService audit = new();
    private readonly FakeAppConfigurationService configuration = new();

    private RadioAdminService Service() =>
        new(stations, translations, unitOfWork, audit, configuration, new FakeSecurityManager());

    private static RadioStationInput Station(string key = "quran-radio-sa") => new()
    {
        Key = key,
        StreamUrl = "https://stream.example.net/quran",
        SortOrder = 0,
        IsPublished = true,
        Translations =
        [
            new TranslationInput
            {
                LanguageCode = "ar",
                Title = "إذاعة القرآن الكريم",
                Body = "هيئة الإذاعة والتلفزيون",
            },
        ],
    };

    [Fact]
    public async Task A_cleartext_stream_is_refused()
    {
        var input = Station();
        input.StreamUrl = "http://stream.example.net/quran";

        var response = await Service().CreateStation(input);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.InsecureStreamUrl, response.ErrorCode);
        Assert.Empty(stations.All);
    }

    [Fact]
    public async Task A_cleartext_logo_is_refused_for_the_same_reason()
    {
        var input = Station();
        input.LogoUrl = "http://cdn.example.net/logo.png";

        var response = await Service().CreateStation(input);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.InsecureStreamUrl, response.ErrorCode);
    }

    [Fact]
    public async Task A_station_without_an_Arabic_name_is_refused()
    {
        var input = Station();
        input.Translations =
        [
            new TranslationInput { LanguageCode = "en", Title = "Holy Qur'an Radio" },
        ];

        var response = await Service().CreateStation(input);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.ValidationError, response.ErrorCode);
    }

    [Fact]
    public async Task Two_stations_cannot_share_a_key()
    {
        var service = Service();
        Assert.True((await service.CreateStation(Station())).Success);

        var response = await service.CreateStation(Station());

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.DuplicateKey, response.ErrorCode);
    }

    [Fact]
    public async Task Every_change_bumps_the_content_version()
    {
        var service = Service();

        var created = await service.CreateStation(Station());
        Assert.Equal(1, configuration.BumpCount);

        var edit = Station();
        edit.IsPublished = false;
        Assert.True((await service.UpdateStation(created.Data!.Id, edit)).Success);
        Assert.Equal(2, configuration.BumpCount);

        Assert.True((await service.DeleteStation(created.Data!.Id)).Success);
        Assert.Equal(3, configuration.BumpCount);
    }

    [Fact]
    public async Task Editing_replaces_the_translation_set_rather_than_merging_it()
    {
        var service = Service();
        var created = await service.CreateStation(Station());

        var edit = Station();
        edit.Translations =
        [
            new TranslationInput
            {
                LanguageCode = "en",
                Title = "Holy Qur'an Radio",
                Body = "Saudi Broadcasting Authority",
            },
            new TranslationInput { LanguageCode = "ar", Title = "إذاعة القرآن" },
        ];

        var response = await service.UpdateStation(created.Data!.Id, edit);

        Assert.True(response.Success);
        Assert.Equal(["ar", "en"], response.Data!.TranslatedLanguages);

        // The broadcaster was dropped from the Arabic tab, so it is gone — an
        // absent field means removed, as everywhere else in the console.
        var arabic = response.Data!.Translations.Single(t => t.LanguageCode == "ar");
        Assert.Null(arabic.Body);
    }

    [Fact]
    public async Task A_missing_station_is_reported_rather_than_thrown()
    {
        var response = await Service().GetStation(404);

        Assert.False(response.Success);
        Assert.Equal(ErrorCode.RadioStationNotFound, response.ErrorCode);
    }
}
