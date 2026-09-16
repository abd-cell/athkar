using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Content;

public class CategoryTranslation : TranslationEntity
{
    public int CategoryId { get; set; }
    public AthkarCategory? Category { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>One line under the title in the index. Optional.</summary>
    public string? Description { get; set; }
}
