using Athkar.Areas.Domain.Staff;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Audit;

/// <summary>
/// One recorded administrative action.
///
/// Written explicitly by each mutating service rather than by a request filter.
/// A filter can record that a PUT happened; only the service knows that this
/// particular PUT unpublished a dhikr whose grading was disputed — and on a
/// project whose credibility is its content, that is the whole point of keeping
/// a log.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Dotted action name from <c>AuditActions</c>.</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;
    public int? EntityId { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>JSON snapshot before the change, where the service had one to hand.</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON snapshot after.</summary>
    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }
}
