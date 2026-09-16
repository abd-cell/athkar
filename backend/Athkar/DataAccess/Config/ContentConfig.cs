using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Content;
using Athkar.Shareds.Constants;

namespace Athkar.DataAccess.Config;

public class AthkarCategoryConfig : IEntityTypeConfiguration<AthkarCategory>
{
    public void Configure(EntityTypeBuilder<AthkarCategory> builder)
    {
        builder.ToTable("AthkarCategories");

        builder.Property(x => x.Key).HasMaxLength(ContentRules.MaxCategoryKeyLength).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(64);

        builder.HasIndex(x => x.Key).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.SortOrder);

        builder.HasMany(x => x.Translations).WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: removing a chapter must not silently take its
        // adhkar with it. The service refuses with CategoryNotEmpty instead.
        builder.HasMany(x => x.Adhkar).WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CategoryTranslationConfig : IEntityTypeConfiguration<CategoryTranslation>
{
    public void Configure(EntityTypeBuilder<CategoryTranslation> builder)
    {
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasIndex(x => new { x.CategoryId, x.LanguageCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class DhikrConfig : IEntityTypeConfiguration<Dhikr>
{
    public void Configure(EntityTypeBuilder<Dhikr> builder)
    {
        builder.ToTable("Adhkar");

        builder.Property(x => x.ArabicText).HasMaxLength(ContentRules.MaxTextLength).IsRequired();
        builder.Property(x => x.SearchText).HasMaxLength(ContentRules.MaxTextLength).IsRequired();
        builder.Property(x => x.SourceBook).HasMaxLength(ContentRules.MaxReferenceLength);
        builder.Property(x => x.SourceReference).HasMaxLength(ContentRules.MaxReferenceLength);
        builder.Property(x => x.GradedBy).HasMaxLength(ContentRules.MaxReferenceLength);

        builder.HasIndex(x => new { x.CategoryId, x.SortOrder });

        // Search runs `LIKE '%folded%'` against this column. A B-tree cannot
        // serve a leading wildcard, but it can still serve the far commoner
        // prefix search, and the column exists so neither has to fold 5,000 rows
        // at query time.
        builder.HasIndex(x => x.SearchText);

        builder.HasMany(x => x.Translations).WithOne(x => x.Dhikr)
            .HasForeignKey(x => x.DhikrId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DhikrTranslationConfig : IEntityTypeConfiguration<DhikrTranslation>
{
    public void Configure(EntityTypeBuilder<DhikrTranslation> builder)
    {
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Translation).HasMaxLength(ContentRules.MaxTextLength).IsRequired();
        builder.Property(x => x.Transliteration).HasMaxLength(ContentRules.MaxTextLength);
        builder.Property(x => x.Virtue).HasMaxLength(ContentRules.MaxTextLength);

        builder.HasIndex(x => new { x.DhikrId, x.LanguageCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
