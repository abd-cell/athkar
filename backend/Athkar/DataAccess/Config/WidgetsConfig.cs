using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Widgets;

namespace Athkar.DataAccess.Config;

public class WidgetSettingsConfig : IEntityTypeConfiguration<WidgetSettings>
{
    public void Configure(EntityTypeBuilder<WidgetSettings> builder)
    {
        builder.Property(x => x.DefaultWidgetKey).HasMaxLength(64);

        // SetNull, not Cascade: removing a chapter should drop the widget back
        // to following the time of day, not delete the widget configuration.
        builder.HasOne(x => x.DhikrCategory).WithMany()
            .HasForeignKey(x => x.DhikrCategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}


public class WidgetCatalogItemConfig : IEntityTypeConfiguration<WidgetCatalogItem>
{
    public void Configure(EntityTypeBuilder<WidgetCatalogItem> builder)
    {
        builder.Property(x => x.Key).HasMaxLength(64).IsRequired();

        // Unique among live rows only, so a withdrawn entry does not hold its
        // key hostage — an admin who deletes a widget and rebuilds it under the
        // same name is doing the obvious thing, and the filter is what lets them.
        builder.HasIndex(x => x.Key).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.Surface, x.SortOrder });

        builder.HasMany(x => x.Translations).WithOne(x => x.WidgetCatalogItem)
            .HasForeignKey(x => x.WidgetCatalogItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WidgetCatalogItemTranslationConfig : IEntityTypeConfiguration<WidgetCatalogItemTranslation>
{
    public void Configure(EntityTypeBuilder<WidgetCatalogItemTranslation> builder)
    {
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Subtitle).HasMaxLength(400);

        builder.HasIndex(x => new { x.WidgetCatalogItemId, x.LanguageCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
