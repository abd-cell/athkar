namespace Athkar.Shareds.Models.Base;

/// <summary>
/// Root of every persisted entity. Soft-delete everywhere — there is no global
/// query filter, because the repository excludes deleted rows by default and the
/// admin console still needs a way to ask for them back.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModificationDate { get; set; }
    public DateTime? DeletionDate { get; set; }
}

/// <summary>Adds the acting staff user id to the audit columns.</summary>
public abstract class AuditableEntity : BaseEntity
{
    public int? CreatedBy { get; set; }
    public int? ModifiedBy { get; set; }
}

/// <summary>
/// A row whose text exists once per language.
///
/// Named rather than repeated because every translation table here has the same
/// two facts about itself — which parent, which language — and the pair is
/// unique. The parent id stays on the concrete class so the foreign key keeps
/// its own name.
/// </summary>
public abstract class TranslationEntity : BaseEntity
{
    /// <summary>BCP-47 primary subtag, lower-case: "ar", "en", "tr".</summary>
    public string LanguageCode { get; set; } = string.Empty;
}
