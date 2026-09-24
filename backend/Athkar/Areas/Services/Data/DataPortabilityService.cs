using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Athkar.Areas.Services.Audit;
using Athkar.Areas.Services.Configuration;
using Athkar.Areas.Services.Data.Models;
using Athkar.DataAccess;
using Athkar.Shareds.Constants;
using Athkar.Shareds.Json;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Data;

/// <summary>
/// Reads and writes the whole model through <see cref="DatabaseService"/>
/// directly rather than <c>IRepository&lt;T&gt;</c> — the repository is generic in
/// a single, compile-time <c>T</c>, and this has to walk every entity type EF
/// knows about at once. Soft delete's own invariant (never physically remove a
/// row) still holds: import only ever adds or updates, never deletes.
/// </summary>
public class DataPortabilityService : IDataPortabilityService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new UtcDateTimeConverter(), new NullableUtcDateTimeConverter() },
    };

    private static readonly System.Reflection.MethodInfo SetMethod =
        typeof(DbContext).GetMethods()
            .Single(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethod && m.GetParameters().Length == 0);

    private readonly DatabaseService context;
    private readonly IAppConfigurationService configuration;
    private readonly IAuditService auditService;

    public DataPortabilityService(
        DatabaseService context, IAppConfigurationService configuration, IAuditService auditService)
    {
        this.context = context;
        this.configuration = configuration;
        this.auditService = auditService;
    }

    public Task<byte[]> Export()
    {
        var result = new DataExportResult { ExportedAtUtc = DateTime.UtcNow };

        foreach (var entityType in OrderedEntityTypes())
        {
            var properties = ScalarProperties(entityType);
            var rows = new List<Dictionary<string, object?>>();

            // Deliberately bypasses IRepository<T>.Query's soft-delete filter —
            // a backup that quietly drops deleted rows is not a backup. DbContext
            // only exposes the generic Set<T>(), so an unknown-at-compile-time
            // CLR type has to go through it via reflection.
            var set = (System.Collections.IEnumerable)SetMethod.MakeGenericMethod(entityType.ClrType).Invoke(context, null)!;
            foreach (var entity in set)
            {
                var row = new Dictionary<string, object?>();
                foreach (var property in properties)
                    row[property.Name] = property.PropertyInfo!.GetValue(entity);
                rows.Add(row);
            }

            result.Tables[entityType.ClrType.Name] = rows;
        }

        return Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions));
    }

    public async Task<BaseResponse<DataImportResult>> Import(IFormFile file)
    {
        DataImportInput? input;
        await using (var stream = file.OpenReadStream())
            input = await JsonSerializer.DeserializeAsync<DataImportInput>(stream, JsonOptions);

        if (input is null)
            return BaseResponse<DataImportResult>.Fail(ErrorCode.ValidationError);

        var result = new DataImportResult();

        await context.Database.BeginTransactionAsync();
        try
        {
            foreach (var entityType in OrderedEntityTypes())
            {
                if (!input.Tables.TryGetValue(entityType.ClrType.Name, out var rows) || rows.Count == 0)
                    continue;

                result.Tables[entityType.ClrType.Name] = await ImportTable(entityType, rows);
            }

            // One bump for the whole import, matching every other bulk content
            // operation — the counter is a change signal, not a row count.
            await configuration.BumpContentVersion();
            await context.Database.CommitTransactionAsync();
        }
        catch
        {
            await context.Database.RollbackTransactionAsync();
            throw;
        }

        await auditService.LogAsync(AuditActions.DataImport, nameof(DataPortabilityService), null, null, result.Tables);
        return new BaseResponse<DataImportResult>(result);
    }

    private async Task<TableImportSummary> ImportTable(
        IEntityType entityType, List<Dictionary<string, JsonElement>> rows)
    {
        var clrType = entityType.ClrType;
        var primaryKey = entityType.FindPrimaryKey()!.Properties.Single();
        var properties = ScalarProperties(entityType);
        var summary = new TableImportSummary();
        var insertedExplicitId = false;

        foreach (var row in rows)
        {
            var hasId = row.TryGetValue(primaryKey.Name, out var idElement) &&
                idElement.ValueKind != JsonValueKind.Null;
            var existing = hasId ? await context.FindAsync(clrType, idElement.GetInt32()) : null;

            if (existing is not null)
            {
                ApplyProperties(existing, properties.Where(p => p != primaryKey), row);
                summary.Updated++;
                continue;
            }

            var entity = Activator.CreateInstance(clrType)!;
            // Only trust the incoming id when the row named one — otherwise let
            // the identity column assign it, same as a normal insert would.
            ApplyProperties(entity, hasId ? properties : properties.Where(p => p != primaryKey), row);
            context.Add(entity);
            summary.Added++;
            if (hasId) insertedExplicitId = true;
        }

        if (insertedExplicitId)
        {
            // An explicit id on insert only lands if SQL Server is told to allow
            // it, and only one table per connection may have this on at a time —
            // hence bracketing just this table's save rather than the whole import.
            // The identifier comes from EF's own model metadata, never from the
            // request, so building the statement by hand is safe here.
            var table = $"[{entityType.GetSchema() ?? "dbo"}].[{entityType.GetTableName()}]";
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} ON");
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT {table} OFF");
#pragma warning restore EF1002
        }
        else
        {
            await context.SaveChangesAsync();
        }

        return summary;
    }

    private static void ApplyProperties(
        object entity, IEnumerable<IProperty> properties, Dictionary<string, JsonElement> row)
    {
        foreach (var property in properties)
        {
            if (!row.TryGetValue(property.Name, out var element)) continue;
            var value = element.ValueKind == JsonValueKind.Null
                ? null
                : JsonSerializer.Deserialize(element.GetRawText(), property.ClrType, JsonOptions);
            property.PropertyInfo!.SetValue(entity, value);
        }
    }

    private static List<IProperty> ScalarProperties(IEntityType entityType) =>
        entityType.GetProperties().Where(p => p.PropertyInfo is not null).ToList();

    /// <summary>
    /// Parents before children, so a foreign key column is always written after
    /// the row it points to exists. Computed from the model's own foreign keys
    /// rather than <see cref="DatabaseService"/>'s <c>DbSet</c> declaration order,
    /// so it stays correct if that order ever drifts from the relationships.
    /// </summary>
    private List<IEntityType> OrderedEntityTypes()
    {
        var all = context.Model.GetEntityTypes().Where(t => !t.IsOwned()).ToList();
        var visited = new HashSet<IEntityType>();
        var ordered = new List<IEntityType>();

        void Visit(IEntityType entityType)
        {
            if (!visited.Add(entityType)) return;
            foreach (var foreignKey in entityType.GetForeignKeys())
                if (foreignKey.PrincipalEntityType != entityType && all.Contains(foreignKey.PrincipalEntityType))
                    Visit(foreignKey.PrincipalEntityType);
            ordered.Add(entityType);
        }

        foreach (var entityType in all) Visit(entityType);
        return ordered;
    }
}
