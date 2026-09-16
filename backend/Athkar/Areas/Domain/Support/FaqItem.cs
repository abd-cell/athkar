using Athkar.Shareds.Enums;
using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Support;

public class FaqItem : AuditableEntity
{
    public FaqCategory Category { get; set; }

    public int SortOrder { get; set; }

    public bool IsPublished { get; set; } = true;

    public ICollection<FaqTranslation> Translations { get; set; } = [];
}
