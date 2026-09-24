using System.Text.Json;

namespace Athkar.Areas.Services.Data.Models;

/// <summary>
/// Every row of every table, keyed by entity name (e.g. <c>"AthkarCategory"</c>,
/// <c>"Dhikr"</c>) rather than the physical SQL table name, so the JSON reads the
/// same regardless of how the schema maps it.
/// </summary>
public class DataExportResult
{
    public DateTime ExportedAtUtc { get; set; }
    public Dictionary<string, List<Dictionary<string, object?>>> Tables { get; set; } = [];
}

/// <summary>
/// Same shape <see cref="DataExportResult.Tables"/> produces. A row with an
/// <c>Id</c> matching an existing one is updated; anything else is added. A
/// property a row omits is left as whatever it already is (update) or the
/// entity's default (add).
/// </summary>
public class DataImportInput
{
    public Dictionary<string, List<Dictionary<string, JsonElement>>> Tables { get; set; } = [];
}

public class DataImportResult
{
    public Dictionary<string, TableImportSummary> Tables { get; set; } = [];
}

public class TableImportSummary
{
    public int Added { get; set; }
    public int Updated { get; set; }
}
