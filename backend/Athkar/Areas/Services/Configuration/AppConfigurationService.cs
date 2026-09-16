using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Configuration.Models;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Models;
using Athkar.Shareds.Security;
using Entity = Athkar.Areas.Domain.Configuration.AppConfiguration;

namespace Athkar.Areas.Services.Configuration;

/// <summary>
/// Reads and writes the single platform settings row.
///
/// The row is seeded on startup, but <see cref="Current"/> still tolerates its
/// absence: the app fetches this before it can show anything, so a missing row
/// must degrade to the built-in defaults rather than fail the launch.
/// </summary>
public class AppConfigurationService : IAppConfigurationService
{
    private readonly IRepository<Entity> repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IAuditService auditService;
    private readonly ISecurityManager securityManager;

    public AppConfigurationService(
        IRepository<Entity> repository,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ISecurityManager securityManager)
    {
        this.repository = repository;
        this.unitOfWork = unitOfWork;
        this.auditService = auditService;
        this.securityManager = securityManager;
    }

    public async Task<BaseResponse<AppConfigurationOutput>> Get()
    {
        var entity = await Current() ?? new Entity();
        return new BaseResponse<AppConfigurationOutput>(new AppConfigurationOutput(entity));
    }

    public async Task<BaseResponse<AppConfigurationOutput>> Update(AppConfigurationInput input)
    {
        var entity = await Current();
        var isNew = entity is null;
        entity ??= new Entity();

        var before = new AppConfigurationOutput(entity);

        entity.PrimaryColor = NormalizeHex(input.PrimaryColor);
        entity.DefaultCalculationMethod = input.DefaultCalculationMethod;
        entity.DefaultMadhab = input.DefaultMadhab;
        entity.MorningOffsetMinutes = Math.Clamp(input.MorningOffsetMinutes, 0, 180);
        entity.EveningOffsetMinutes = Math.Clamp(input.EveningOffsetMinutes, 0, 180);
        entity.MinimumAppBuild = Math.Max(0, input.MinimumAppBuild);
        entity.ReviewingScholar = Blank(input.ReviewingScholar);
        entity.ReviewingScholarCredential = Blank(input.ReviewingScholarCredential);
        entity.SupportEmail = Blank(input.SupportEmail)?.ToLowerInvariant();
        entity.SupportWebsite = Blank(input.SupportWebsite);
        entity.PrivacyPolicyUrl = Blank(input.PrivacyPolicyUrl);
        entity.AndroidPackageName = Blank(input.AndroidPackageName);
        entity.IosAppStoreId = Blank(input.IosAppStoreId);
        entity.ModifiedBy = securityManager.UserId;

        if (isNew) repository.Create(entity);
        else repository.Update(entity);

        await unitOfWork.SaveAsync();

        await auditService.LogAsync(AuditActions.ConfigurationUpdate, nameof(Entity), entity.Id,
            before, new AppConfigurationOutput(entity));

        return new BaseResponse<AppConfigurationOutput>(new AppConfigurationOutput(entity));
    }

    public async Task<int> BumpContentVersion()
    {
        var entity = await Current();
        if (entity is null)
        {
            // No row yet means the seeder has not run, which in turn means no app
            // has ever synced. Starting at 2 keeps the version strictly rising
            // rather than appearing to go backwards once the seeder does run.
            entity = new Entity { ContentVersion = 2 };
            repository.Create(entity);
        }
        else
        {
            entity.ContentVersion++;
            repository.Update(entity);
        }

        await unitOfWork.SaveAsync();
        return entity.ContentVersion;
    }

    /// <summary>Oldest live row wins, so a stray duplicate can never flip the brand mid-flight.</summary>
    private Task<Entity?> Current() =>
        repository.Query().OrderBy(x => x.Id).FirstOrDefaultAsync();

    private static string? Blank(string? raw)
    {
        var text = raw?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>`#abc` / `abc123` → `#AABBCC`, so clients parse exactly one shape.</summary>
    private static string NormalizeHex(string raw)
    {
        var hex = raw.Trim().TrimStart('#');
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        return $"#{hex.ToUpperInvariant()}";
    }
}
