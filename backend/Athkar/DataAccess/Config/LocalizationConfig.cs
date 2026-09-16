using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Localization;

namespace Athkar.DataAccess.Config;

public class AppLanguageConfig : IEntityTypeConfiguration<AppLanguage>
{
    public void Configure(EntityTypeBuilder<AppLanguage> builder)
    {
        builder.Property(x => x.Code).HasMaxLength(8).IsRequired();
        builder.Property(x => x.NativeName).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EnglishName).HasMaxLength(64).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class UiStringConfig : IEntityTypeConfiguration<UiString>
{
    public void Configure(EntityTypeBuilder<UiString> builder)
    {
        builder.Property(x => x.Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(2000).IsRequired();

        builder.HasOne(x => x.Language).WithMany()
            .HasForeignKey(x => x.LanguageId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.LanguageId, x.Key })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
