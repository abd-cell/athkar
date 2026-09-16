using Athkar.Shareds.Enums;

namespace Athkar.Areas.Services.Management.Models;

/// <summary>
/// The dashboard, as one payload.
///
/// One call because a dashboard that arrives in seven pieces flickers through
/// seven loading states, and none of these figures is expensive on its own.
/// </summary>
public class DashboardOutput
{
    public int TotalDevices { get; set; }
    public int ActiveDevices { get; set; }
    public int ReachableDevices { get; set; }

    public int PublishedCategories { get; set; }
    public int PublishedAdhkar { get; set; }

    /// <summary>
    /// Drafts with no source. The number an editor should be looking at — it is
    /// the backlog standing between the corpus and publication.
    /// </summary>
    public int UnsourcedDrafts { get; set; }

    public int EnabledLanguages { get; set; }
    public int ActiveReminders { get; set; }
    public int OpenFeedback { get; set; }

    /// <summary>Pushes attempted in the last 24 hours, by outcome.</summary>
    public Dictionary<string, int> PushLastDay { get; set; } = [];

    /// <summary>New installs per day for the last 30, oldest first.</summary>
    public List<DailyCount> Installs { get; set; } = [];

    public Dictionary<string, int> DevicesByPlatform { get; set; } = [];
    public Dictionary<string, int> DevicesByLanguage { get; set; } = [];
}

public record DailyCount(DateOnly Day, int Count);

public class AuditOutput
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string? UserName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ApiLogOutput
{
    public int Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public int? ErrorCode { get; set; }
    public long DurationMs { get; set; }
    public string? DeviceKey { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
