using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Support;

public class FaqTranslation : TranslationEntity
{
    public int FaqItemId { get; set; }
    public FaqItem? FaqItem { get; set; }

    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}
