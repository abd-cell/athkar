using Microsoft.Extensions.Options;
using Athkar.Areas.Domain.Quran;
using Athkar.Areas.Services.Quran;
using Athkar.Areas.Services.Quran.Models;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Enums;
using Athkar.Shareds.Files;
using Athkar.Shareds.Models;
using Athkar.Shareds.Models.Config;
using Athkar.Tests.TestDoubles;

namespace Athkar.Tests;

/// <summary>
/// Publishing a mushaf, and the two doors that were missing from it.
///
/// The invariant worth guarding is that <b>exactly one package per edition is
/// offered at a time</b> — an app asking "is there anything newer for the mushaf
/// I am reading" must get one answer. It used to be one package full stop; the
/// edition is what lets a reader choose between mushafs without publishing one
/// taking the others off the shelf.
///
/// The rest of this file is the way back out: publishing used to be one-way, so
/// a package could only ever be replaced, never withdrawn, and since a published
/// package also cannot be deleted, a mushaf found to have a defect had no exit
/// at all.
/// </summary>
public class QuranPackageAdminTests
{
    private readonly InMemoryRepository<QuranPackage> packages = new();
    private readonly FakeUnitOfWork unitOfWork = new();
    private readonly FakeFileStorage storage = new();
    private readonly FakeAuditService audit = new();

    private QuranService Service() => new(
        packages, unitOfWork, storage, audit,
        new FakeSecurityManager(),
        Options.Create(new StorageSettings()));

    private QuranPackage Package(
        int version,
        bool published = false,
        string edition = "hafs-uthmani",
        bool isDefault = false) =>
        new()
        {
            Edition = edition,
            Name = "مصحف " + edition,
            IsDefault = isDefault,
            Version = version,
            FileName = $"mushaf-v{version}.db",
            StorageKey = $"quran/{version}.db",
            Sha256 = new string('a', 64),
            SizeBytes = 1024,
            Script = QuranScript.Uthmani,
            IsPublished = published,
            PublishedAt = published ? DateTime.UtcNow : null,
        };

    [Fact]
    public async Task Publishing_withdraws_whatever_was_published_before_of_the_same_edition()
    {
        // The app asks one question per mushaf — "is there anything newer than
        // what I have" — and two published packages of one edition would be two
        // answers.
        var old = Package(1, published: true);
        var fresh = Package(2);
        packages.Seed(old, fresh);

        await Service().Publish(fresh.Id);

        Assert.False(old.IsPublished);
        Assert.True(fresh.IsPublished);
        Assert.Single(packages.All.Where(p => p.IsPublished));
    }

    [Fact]
    public async Task Publishing_one_mushaf_leaves_the_other_mushafs_on_the_shelf()
    {
        // The regression this guards is the whole point of editions: withdrawing
        // *everything* else on publish — which is what this did when one mushaf
        // was all there was — would take Warsh offline every time Hafs was
        // updated, and every reader following Warsh would be told their mushaf
        // no longer exists.
        var warsh = Package(1, published: true, edition: "warsh", isDefault: true);
        var hafs = Package(1, edition: "hafs-uthmani");
        packages.Seed(warsh, hafs);

        await Service().Publish(hafs.Id);

        Assert.True(warsh.IsPublished);
        Assert.True(hafs.IsPublished);
        Assert.Equal(2, packages.All.Count(p => p.IsPublished));

        // And the default did not move: it belongs to the reader-facing shelf,
        // not to whichever package was touched last.
        Assert.True(warsh.IsDefault);
        Assert.False(hafs.IsDefault);
    }

    [Fact]
    public async Task The_first_mushaf_published_becomes_the_default_without_being_told()
    {
        // A fresh install asks for "the default". If publishing the only mushaf
        // there is left that unanswered, the app would show nothing while a
        // perfectly good file sat published.
        var only = Package(1);
        packages.Seed(only);

        await Service().Publish(only.Id);

        Assert.True(only.IsDefault);
    }

    [Fact]
    public async Task A_new_version_of_the_default_edition_inherits_the_default()
    {
        // Otherwise updating the default mushaf would silently leave every
        // device that names no edition with nothing to download.
        var old = Package(1, published: true, isDefault: true);
        var fresh = Package(2);
        packages.Seed(old, fresh);

        await Service().Publish(fresh.Id);

        Assert.False(old.IsDefault);
        Assert.True(fresh.IsDefault);
    }

    [Fact]
    public async Task Withdrawing_the_default_hands_it_to_a_mushaf_that_is_still_published()
    {
        // A default nobody can download points every fresh install at a file
        // that is not there — and the app has no way to tell that apart from
        // "nothing is published at all".
        var warsh = Package(1, published: true, edition: "warsh", isDefault: true);
        var hafs = Package(1, published: true, edition: "hafs-uthmani");
        warsh.PublishedAt = DateTime.UtcNow.AddDays(-2);
        hafs.PublishedAt = DateTime.UtcNow.AddDays(-1);
        packages.Seed(warsh, hafs);

        await Service().Unpublish(warsh.Id);

        Assert.False(warsh.IsDefault);
        Assert.True(hafs.IsDefault);
    }

    [Fact]
    public async Task Only_a_published_package_can_be_made_the_default()
    {
        // A default that is not published would leave every fresh install with
        // no mushaf and no error to show for it.
        var package = Package(1);
        packages.Seed(package);

        var response = await Service().SetDefault(package.Id);

        Assert.False(response.Success);
        Assert.False(package.IsDefault);
        Assert.Empty(audit.Actions);
    }

    [Fact]
    public async Task Making_a_mushaf_the_default_takes_it_from_the_one_that_had_it()
    {
        var warsh = Package(1, published: true, edition: "warsh", isDefault: true);
        var hafs = Package(1, published: true, edition: "hafs-uthmani");
        packages.Seed(warsh, hafs);

        var response = await Service().SetDefault(hafs.Id);

        Assert.True(response.Success);
        Assert.True(hafs.IsDefault);
        Assert.False(warsh.IsDefault);
        Assert.Single(packages.All.Where(p => p.IsDefault));
        Assert.Contains(AuditActions.QuranPackageSetDefault, audit.Actions);
    }

    [Fact]
    public async Task A_device_that_names_no_edition_is_answered_with_the_default()
    {
        var warsh = Package(7, published: true, edition: "warsh");
        var hafs = Package(2, published: true, edition: "hafs-uthmani", isDefault: true);
        packages.Seed(warsh, hafs);

        var response = await Service().CheckVersion(knownVersion: null);

        Assert.True(response.Success);
        // Not Warsh, whose version number is higher: before editions the answer
        // was "the highest version published", and that would now hand a fresh
        // install whichever mushaf happened to have been updated most recently.
        Assert.Equal("hafs-uthmani", response.Data!.Edition);
        Assert.Equal(2, response.Data.Version);
    }

    [Fact]
    public async Task A_device_following_an_edition_that_is_gone_is_told_so_rather_than_handed_another()
    {
        // Silently answering with the default would leave a device that has been
        // reading Warsh downloading Hafs and storing it under the name Warsh — a
        // mushaf swapped underneath a reader with nothing on screen to say so.
        var hafs = Package(2, published: true, edition: "hafs-uthmani", isDefault: true);
        packages.Seed(hafs);

        var response = await Service().CheckVersion(knownVersion: 1, edition: "warsh");

        Assert.True(response.Success);
        Assert.Null(response.Data!.Version);
        Assert.Null(response.Data.Edition);
    }

    [Fact]
    public async Task The_editions_list_offers_the_published_mushafs_default_first()
    {
        var draft = Package(1, edition: "naskh");
        var hafs = Package(2, published: true, edition: "hafs-uthmani", isDefault: true);
        var warsh = Package(1, published: true, edition: "warsh");
        packages.Seed(draft, hafs, warsh);

        var response = await Service().Editions();

        Assert.True(response.Success);
        Assert.Equal(2, response.Data!.Count);
        Assert.Equal("hafs-uthmani", response.Data[0].Edition);
        Assert.DoesNotContain(response.Data, e => e.Edition == "naskh");
    }

    [Fact]
    public async Task A_package_can_be_withdrawn_leaving_nothing_published()
    {
        // The door that was missing. A mushaf found to have a defect has to be
        // removable without another one standing ready to replace it.
        var package = Package(1, published: true);
        packages.Seed(package);

        var response = await Service().Unpublish(package.Id);

        Assert.True(response.Success);
        Assert.False(package.IsPublished);
        Assert.Null(package.PublishedAt);
        Assert.Empty(packages.All.Where(p => p.IsPublished));
        Assert.Contains(AuditActions.QuranPackageUnpublish, audit.Actions);
    }

    [Fact]
    public async Task Withdrawing_then_deleting_is_the_way_a_published_package_leaves()
    {
        // Delete refuses a published package on purpose. Before withdrawal
        // existed, that made publishing a one-way door.
        var package = Package(1, published: true);
        packages.Seed(package);

        Assert.False((await Service().Delete(package.Id)).Success);

        await Service().Unpublish(package.Id);

        Assert.True((await Service().Delete(package.Id)).Success);
        Assert.True(package.IsDeleted);
    }

    [Fact]
    public async Task Withdrawing_something_that_is_not_published_is_refused_rather_than_ignored()
    {
        // Silence here would read as success, and an admin would believe they
        // had taken down a mushaf that is still being downloaded.
        var package = Package(1);
        packages.Seed(package);

        var response = await Service().Unpublish(package.Id);

        Assert.False(response.Success);
        Assert.Empty(audit.Actions);
    }

    [Fact]
    public async Task Editing_changes_what_a_package_says_and_never_what_it_is()
    {
        // The version, the bytes, the size and the checksum are how an installed
        // copy identifies itself. A label may be corrected; an identity may not.
        var package = Package(3, published: true);
        packages.Seed(package);

        var response = await Service().Edit(package.Id, new QuranPackageEditInput
        {
            Name = "المصحف العثماني · حفص",
            Script = QuranScript.IndoPak,
            HasWaqfAnnotations = true,
            ReleaseNotes = "  تصحيح وصف الرسم  ",
        });

        Assert.True(response.Success);
        Assert.Equal(QuranScript.IndoPak, package.Script);
        Assert.True(package.HasWaqfAnnotations);
        Assert.Equal("تصحيح وصف الرسم", package.ReleaseNotes);

        Assert.Equal(3, package.Version);
        Assert.Equal("hafs-uthmani", package.Edition);
        Assert.Equal(new string('a', 64), package.Sha256);
        Assert.Equal(1024, package.SizeBytes);
        Assert.Equal("quran/3.db", package.StorageKey);

        // Still published: correcting a label must not take a mushaf offline.
        Assert.True(package.IsPublished);
        Assert.Contains(AuditActions.QuranPackageUpdate, audit.Actions);
    }

    [Fact]
    public async Task An_admin_can_read_back_an_unpublished_package()
    {
        // The app's download offers only what is published. An admin checking
        // that an upload is the file they meant needs the one that is not.
        var package = Package(1);
        packages.Seed(package);
        storage.Files[package.StorageKey] = [1, 2, 3];

        var file = await Service().OpenForAdmin(package.Id);

        Assert.NotNull(file);
        Assert.Equal("mushaf-v1.db", file.Value.FileName);
        Assert.Equal(package.Sha256, file.Value.Sha256);
    }
}

/// <summary>
/// Storage as a dictionary. Enough for the package rules, which are entirely
/// about rows — the bytes only ever have to come back.
/// </summary>
public class FakeFileStorage : IFileStorage
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task<string> SaveAsync(string folder, string extension, Stream content,
        CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        var key = $"{folder}/{Guid.NewGuid():N}{extension}";
        Files[key] = buffer.ToArray();

        return key;
    }

    public Stream? OpenRead(string key) =>
        Files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null;

    public long? SizeOf(string key) =>
        Files.TryGetValue(key, out var bytes) ? bytes.Length : null;

    public Task DeleteAsync(string key)
    {
        Files.Remove(key);
        return Task.CompletedTask;
    }
}
