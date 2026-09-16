using Athkar.Shareds.Models.Base;

namespace Athkar.Areas.Domain.Radio;

public class RadioStationTranslation : TranslationEntity
{
    public int StationId { get; set; }
    public RadioStation? Station { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Who broadcasts it — «هيئة الإذاعة والتلفزيون». Translated rather than
    /// stored once, because a broadcaster is a name and names are written
    /// differently in each language the app speaks. Optional.
    /// </summary>
    public string? Provider { get; set; }
}
