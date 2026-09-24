using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Recitations;
using Athkar.Shareds.Constants;

namespace Athkar.DataAccess.Config;

public class ReciterConfig : IEntityTypeConfiguration<Reciter>
{
    public void Configure(EntityTypeBuilder<Reciter> builder)
    {
        builder.ToTable("Reciters");

        builder.Property(x => x.Key).HasMaxLength(ContentRules.MaxCategoryKeyLength).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(ContentRules.MaxUrlLength);

        builder.HasIndex(x => x.Key).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.ExternalId);
        builder.HasIndex(x => x.SortOrder);

        builder.HasMany(x => x.Translations).WithOne(x => x.Reciter)
            .HasForeignKey(x => x.ReciterId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Recitations).WithOne(x => x.Reciter)
            .HasForeignKey(x => x.ReciterId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReciterTranslationConfig : IEntityTypeConfiguration<ReciterTranslation>
{
    public void Configure(EntityTypeBuilder<ReciterTranslation> builder)
    {
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.ReciterId, x.LanguageCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class RecitationConfig : IEntityTypeConfiguration<Recitation>
{
    public void Configure(EntityTypeBuilder<Recitation> builder)
    {
        builder.ToTable("Recitations");

        builder.Property(x => x.ServerUrl).HasMaxLength(ContentRules.MaxUrlLength).IsRequired();
        // 114 surahs, comma-separated, is under 500 characters; the headroom is
        // for a publisher that one day pads its list.
        builder.Property(x => x.SurahList).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.SourceName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SourceUrl).HasMaxLength(ContentRules.MaxUrlLength);

        builder.HasIndex(x => x.ExternalId);

        builder.HasMany(x => x.Translations).WithOne(x => x.Recitation)
            .HasForeignKey(x => x.RecitationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RecitationTranslationConfig : IEntityTypeConfiguration<RecitationTranslation>
{
    public void Configure(EntityTypeBuilder<RecitationTranslation> builder)
    {
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();

        builder.HasIndex(x => new { x.RecitationId, x.LanguageCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
