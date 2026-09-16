namespace Athkar.Shareds.Enums;

/// <summary>
/// Which typeface a Qur'an package is drawn for. Stored on the package because
/// an Uthmani text laid out in a Naskh face loses the very marks it was
/// digitised to carry.
/// </summary>
public enum QuranScript
{
    /// <summary>King Fahd Complex Uthmani orthography.</summary>
    Uthmani = 1,

    /// <summary>Hafs, in the Indo-Pak conventions.</summary>
    IndoPak = 2,

    /// <summary>Plain Naskh — imla'i spelling, for reading rather than recitation.</summary>
    Naskh = 3,
}
