using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Athkar.Areas.Domain.Quran;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Quran.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Files;
using Athkar.Shareds.Models;
using Athkar.Shareds.Models.Config;
using Athkar.Shareds.Security;

namespace Athkar.Areas.Services.Quran;

public class QuranService : IQuranService
{
    private const string Folder = "quran";

    private readonly IRepository<QuranPackage> packages;
    private readonly IUnitOfWork unitOfWork;
    private readonly IFileStorage storage;
    private readonly IAuditService auditService;
    private readonly ISecurityManager securityManager;
    private readonly StorageSettings settings;

    public QuranService(
        IRepository<QuranPackage> packages,
        IUnitOfWork unitOfWork,
        IFileStorage storage,
        IAuditService auditService,
        ISecurityManager securityManager,
        IOptions<StorageSettings> options)
    {
        this.packages = packages;
        this.unitOfWork = unitOfWork;
        this.storage = storage;
        this.auditService = auditService;
        this.securityManager = securityManager;
        settings = options.Value;
    }

    public async Task<BaseResponse<QuranVersionOutput>> CheckVersion(int? knownVersion, string? edition = null)
    {
        var published = await Published(edition);

        if (published is null)
            return new BaseResponse<QuranVersionOutput>(new QuranVersionOutput());

        return new BaseResponse<QuranVersionOutput>(new QuranVersionOutput
        {
            Edition = published.Edition,
            Name = published.Name,
            Version = published.Version,
            UpdateAvailable = knownVersion is null || knownVersion < published.Version,
            SizeBytes = published.SizeBytes,
            Sha256 = published.Sha256,
            Script = published.Script,
            HasWaqfAnnotations = published.HasWaqfAnnotations,
            ReleaseNotes = published.ReleaseNotes,
            PublishedAt = published.PublishedAt,
        });
    }

    public async Task<BaseResponse<List<QuranEditionOutput>>> Editions()
    {
        var rows = await packages.Query()
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync();

        return new BaseResponse<List<QuranEditionOutput>>(
            [.. rows.Select(p => new QuranEditionOutput(p))]);
    }

    public async Task<(Stream Content, string FileName, string Sha256)?> OpenPublished(string? edition = null)
    {
        var published = await Published(edition);
        if (published is null) return null;

        var content = storage.OpenRead(published.StorageKey);
        return content is null ? null : (content, published.FileName, published.Sha256);
    }

    public async Task RecordDownload(string? edition = null)
    {
        var published = await Published(edition);
        if (published is null) return;

        published.DownloadCount++;
        packages.Update(published);
        await unitOfWork.SaveAsync();
    }

    public async Task<BaseResponse<List<QuranPackageOutput>>> List()
    {
        var rows = await packages.Query()
            .OrderByDescending(p => p.Version)
            .ToListAsync();

        return new BaseResponse<List<QuranPackageOutput>>(
            [.. rows.Select(p => new QuranPackageOutput(p))]);
    }

    public async Task<BaseResponse<QuranPackageOutput>> Upload(QuranUploadInput input, IFormFile file)
    {
        if (file.Length == 0)
            return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.ValidationError, "The file is empty.");

        if (file.Length > settings.MaxQuranPackageMb * 1024L * 1024L)
            return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.FileTooLarge);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".db" or ".sqlite" or ".sqlite3" or ".zip"))
            return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.UnsupportedFileType);

        var edition = input.Edition.Trim().ToLowerInvariant();

        // Checked against every row of this edition, deleted ones included: a
        // version number is what an installed app compares against, so re-using
        // one would leave that app holding a different file under a number it
        // thinks it has. Scoped to the edition because two mushafs are two
        // sequences — Warsh reaching version 3 says nothing about Hafs.
        if (await packages.AnyAsync(
                p => p.Edition == edition && p.Version == input.Version, includeDeleted: true))
            return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.DuplicateQuranVersion);

        await using var content = file.OpenReadStream();
        var key = await storage.SaveAsync(Folder, extension, content);

        var hash = await ComputeSha256(key);

        if (!string.IsNullOrWhiteSpace(input.ExpectedSha256) &&
            !string.Equals(hash, input.ExpectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            // Removed rather than kept as a failed row: a file whose bytes are
            // not what the uploader meant to send is of no use to anyone, and
            // leaving it on disk is how a storage volume fills up.
            await storage.DeleteAsync(key);
            return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.ChecksumMismatch);
        }

        var package = new QuranPackage
        {
            Edition = edition,
            Name = input.Name.Trim(),
            Version = input.Version,
            StorageKey = key,
            FileName = Path.GetFileName(file.FileName),
            SizeBytes = storage.SizeOf(key) ?? file.Length,
            Sha256 = hash,
            Script = input.Script,
            HasWaqfAnnotations = input.HasWaqfAnnotations,
            ReleaseNotes = string.IsNullOrWhiteSpace(input.ReleaseNotes) ? null : input.ReleaseNotes.Trim(),
            IsPublished = false,
            CreatedBy = securityManager.UserId,
        };

        await packages.AddAsync(package);
        await unitOfWork.SaveAsync();

        var output = new QuranPackageOutput(package);
        await auditService.LogAsync(AuditActions.QuranPackageUpload, nameof(QuranPackage),
            package.Id, null, output);

        return new BaseResponse<QuranPackageOutput>(output);
    }

    public async Task<BaseResponse<QuranPackageOutput>> Publish(int id)
    {
        var package = await packages.GetByIdAsync(id);
        if (package is null) return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.QuranPackageNotFound);

        // Only this edition's own predecessor steps down. Withdrawing every
        // other package here — which is what this did when one mushaf was all
        // there was — would take every other mushaf off the shelf each time one
        // of them was updated.
        var others = await packages
            .Where(p => p.IsPublished && p.Edition == package.Edition && p.Id != id)
            .ToListAsync();

        foreach (var other in others)
        {
            other.IsPublished = false;

            // The default follows the edition, not the file: the package being
            // replaced hands the flag to the one replacing it.
            if (other.IsDefault)
            {
                other.IsDefault = false;
                package.IsDefault = true;
            }

            packages.Update(other);
        }

        // The first mushaf on the shelf is the default by arithmetic rather than
        // by an admin remembering to say so — otherwise a fresh install would
        // ask for "the default", be told there is none, and show nothing while a
        // perfectly good mushaf sat published.
        if (!await packages.AnyAsync(p => p.IsPublished && p.IsDefault && p.Id != id))
            package.IsDefault = true;

        package.IsPublished = true;
        package.PublishedAt = DateTime.UtcNow;
        package.ModifiedBy = securityManager.UserId;
        packages.Update(package);

        await unitOfWork.SaveAsync();

        var output = new QuranPackageOutput(package);
        await auditService.LogAsync(AuditActions.QuranPackagePublish, nameof(QuranPackage),
            package.Id, null, output);

        return new BaseResponse<QuranPackageOutput>(output);
    }

    public async Task<BaseResponse<QuranPackageOutput>> SetDefault(int id)
    {
        var package = await packages.GetByIdAsync(id);
        if (package is null) return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.QuranPackageNotFound);

        if (!package.IsPublished)
            return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.Conflict,
                "Publish this package before making it the default.");

        if (package.IsDefault) return new BaseResponse<QuranPackageOutput>(new QuranPackageOutput(package));

        var before = new QuranPackageOutput(package);

        var others = await packages.Where(p => p.IsDefault && p.Id != id).ToListAsync();
        foreach (var other in others)
        {
            other.IsDefault = false;
            packages.Update(other);
        }

        package.IsDefault = true;
        package.ModifiedBy = securityManager.UserId;
        packages.Update(package);

        await unitOfWork.SaveAsync();

        var output = new QuranPackageOutput(package);
        await auditService.LogAsync(AuditActions.QuranPackageSetDefault, nameof(QuranPackage),
            package.Id, before, output);

        return new BaseResponse<QuranPackageOutput>(output);
    }

    public async Task<BaseResponse<QuranPackageOutput>> Unpublish(int id)
    {
        var package = await packages.GetByIdAsync(id);
        if (package is null) return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.QuranPackageNotFound);

        if (!package.IsPublished)
            return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.Conflict,
                "This package is not published.");

        var before = new QuranPackageOutput(package);

        package.IsPublished = false;
        package.PublishedAt = null;
        package.ModifiedBy = securityManager.UserId;
        packages.Update(package);

        // A withdrawn default would point every device that names no edition at
        // a file it can no longer have. The oldest remaining published mushaf
        // takes over; if there is none, nothing is published at all and the
        // version check says so, which the app already handles.
        if (package.IsDefault)
        {
            package.IsDefault = false;

            var heir = await packages.Query()
                .Where(p => p.IsPublished && p.Id != id)
                .OrderBy(p => p.PublishedAt)
                .FirstOrDefaultAsync();

            if (heir is not null)
            {
                heir.IsDefault = true;
                packages.Update(heir);
            }
        }

        await unitOfWork.SaveAsync();

        // Nothing is published now, and the app's version check says so rather
        // than offering a file that is no longer meant to be read. An install
        // that already has it keeps it — the download was the delivery.
        var output = new QuranPackageOutput(package);
        await auditService.LogAsync(AuditActions.QuranPackageUnpublish, nameof(QuranPackage),
            package.Id, before, output);

        return new BaseResponse<QuranPackageOutput>(output);
    }

    public async Task<BaseResponse<QuranPackageOutput>> Edit(int id, QuranPackageEditInput input)
    {
        var package = await packages.GetByIdAsync(id);
        if (package is null) return BaseResponse<QuranPackageOutput>.Fail(ErrorCode.QuranPackageNotFound);

        var before = new QuranPackageOutput(package);

        // Only the describing fields. Version, bytes, size and checksum are
        // untouched here by construction — they are what an installed copy
        // identifies itself by.
        package.Name = input.Name.Trim();
        package.Script = input.Script;
        package.HasWaqfAnnotations = input.HasWaqfAnnotations;
        package.ReleaseNotes = string.IsNullOrWhiteSpace(input.ReleaseNotes)
            ? null
            : input.ReleaseNotes.Trim();

        package.ModifiedBy = securityManager.UserId;
        packages.Update(package);

        await unitOfWork.SaveAsync();

        var output = new QuranPackageOutput(package);
        await auditService.LogAsync(AuditActions.QuranPackageUpdate, nameof(QuranPackage),
            package.Id, before, output);

        return new BaseResponse<QuranPackageOutput>(output);
    }

    public async Task<(Stream Content, string FileName, string Sha256)?> OpenForAdmin(int id)
    {
        var package = await packages.GetByIdAsync(id);
        if (package is null) return null;

        var content = storage.OpenRead(package.StorageKey);
        return content is null ? null : (content, package.FileName, package.Sha256);
    }

    public async Task<BaseResponse> Delete(int id)
    {
        var package = await packages.GetByIdAsync(id);
        if (package is null) return BaseResponse.Fail(ErrorCode.QuranPackageNotFound);

        if (package.IsPublished)
            return BaseResponse.Fail(ErrorCode.Conflict,
                "Publish another package before removing this one.");

        await storage.DeleteAsync(package.StorageKey);
        packages.SoftDelete(package);
        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.QuranPackageDelete, nameof(QuranPackage),
            id, new QuranPackageOutput(package), null);

        return new BaseResponse();
    }

    /// <summary>
    /// The published package of an edition, or of the default edition when none
    /// is named.
    /// </summary>
    /// <remarks>
    /// An edition that is named but has nothing published returns null rather
    /// than quietly falling back to the default: a device that has been
    /// following Warsh must be told Warsh is gone, not handed Hafs under the
    /// name it stored.
    /// </remarks>
    private Task<QuranPackage?> Published(string? edition = null)
    {
        var query = packages.Query().Where(p => p.IsPublished);

        query = string.IsNullOrWhiteSpace(edition)
            ? query.Where(p => p.IsDefault)
            : query.Where(p => p.Edition == edition.Trim().ToLower());

        return query.OrderByDescending(p => p.Version).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Hashes the stored file rather than the request stream, so the digest
    /// describes what is actually on disk — which is the thing the app will
    /// later check its download against.
    /// </summary>
    private async Task<string> ComputeSha256(string key)
    {
        await using var stored = storage.OpenRead(key)
            ?? throw new AppException(ErrorCode.UnknownError, "The upload could not be read back.");

        var hash = await SHA256.HashDataAsync(stored);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
