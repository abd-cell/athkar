using Microsoft.AspNetCore.Http;
using Athkar.Areas.Services.Data.Models;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models;

namespace Athkar.Areas.Services.Data;

/// <summary>
/// Whole-database export and restore, driven entirely off EF's own model
/// rather than hand-written per entity — a table added to
/// <see cref="Athkar.DataAccess.DatabaseService"/> is covered the day it is
/// added, not the day someone remembers to extend this file.
/// </summary>
[ScopedInjectable]
public interface IDataPortabilityService
{
    /// <summary>
    /// The raw bytes of a JSON file: every row of every table, including
    /// soft-deleted ones, for full fidelity.
    /// </summary>
    Task<byte[]> Export();

    /// <summary>
    /// Reads an uploaded export file. Adds a row for every id not already
    /// present, updates one for every id that is.
    /// </summary>
    Task<BaseResponse<DataImportResult>> Import(IFormFile file);
}
