using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Quran;

namespace Athkar.DataAccess.Config;

public class QuranPackageConfig : IEntityTypeConfiguration<QuranPackage>
{
    public void Configure(EntityTypeBuilder<QuranPackage> builder)
    {
        builder.Property(x => x.Edition).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ReleaseNotes).HasMaxLength(2000);

        // Versions are immutable once uploaded, so the uniqueness is not
        // filtered on IsDeleted: a deleted package's number must not come back
        // on a different file, or an app holding the old one would never update.
        //
        // Scoped to the edition, because two mushafs are two sequences — Warsh
        // reaching version 3 says nothing about Hafs, and a global index would
        // make the second mushaf uploaded start at whatever the first had
        // reached.
        builder.HasIndex(x => new { x.Edition, x.Version }).IsUnique();

        // The list the app asks for on every Qur'an screen: published rows,
        // default first.
        builder.HasIndex(x => new { x.IsPublished, x.IsDefault });
    }
}
