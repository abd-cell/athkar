using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Radio;
using Athkar.Shareds.Constants;

namespace Athkar.DataAccess.Config;

public class RadioStationConfig : IEntityTypeConfiguration<RadioStation>
{
    public void Configure(EntityTypeBuilder<RadioStation> builder)
    {
        builder.ToTable("RadioStations");

        builder.Property(x => x.Key).HasMaxLength(ContentRules.MaxCategoryKeyLength).IsRequired();
        builder.Property(x => x.StreamUrl).HasMaxLength(ContentRules.MaxUrlLength).IsRequired();
        builder.Property(x => x.LogoUrl).HasMaxLength(ContentRules.MaxUrlLength);

        builder.HasIndex(x => x.Key).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => x.SortOrder);

        builder.HasMany(x => x.Translations).WithOne(x => x.Station)
            .HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RadioStationTranslationConfig : IEntityTypeConfiguration<RadioStationTranslation>
{
    public void Configure(EntityTypeBuilder<RadioStationTranslation> builder)
    {
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Provider).HasMaxLength(200);

        builder.HasIndex(x => new { x.StationId, x.LanguageCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
