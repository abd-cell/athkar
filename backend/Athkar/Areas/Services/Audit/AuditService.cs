using System.Text.Json;
using Athkar.DataAccess.Repositories;
using Athkar.DataAccess.UnitOfWorks;
using Athkar.Shareds.Security;
using Entity = Athkar.Areas.Domain.Audit.AuditLog;

namespace Athkar.Areas.Services.Audit;

public class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Snapshots are read by a person in the CMS, not parsed, so the cost of
        // indentation buys readability in the one place it is looked at.
        WriteIndented = false,
    };

    private readonly IRepository<Entity> repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly ISecurityManager securityManager;
    private readonly IHttpContextAccessor http;
    private readonly ILogger<AuditService> logger;

    public AuditService(
        IRepository<Entity> repository,
        IUnitOfWork unitOfWork,
        ISecurityManager securityManager,
        IHttpContextAccessor http,
        ILogger<AuditService> logger)
    {
        this.repository = repository;
        this.unitOfWork = unitOfWork;
        this.securityManager = securityManager;
        this.http = http;
        this.logger = logger;
    }

    public async Task LogAsync(string action, string entityName, int? entityId,
        object? oldValue = null, object? newValue = null)
    {
        try
        {
            repository.Create(new Entity
            {
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                UserId = securityManager.UserId,
                OldValue = Serialize(oldValue),
                NewValue = Serialize(newValue),
                IpAddress = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            });

            await unitOfWork.SaveAsync();
        }
        catch (Exception ex)
        {
            // The action itself has already committed. Failing the request now
            // would tell the admin their edit did not happen when it did, which
            // is a worse outcome than a gap in the log.
            logger.LogError(ex, "Failed to write audit entry {Action} for {Entity} {Id}.",
                action, entityName, entityId);
        }
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
}
