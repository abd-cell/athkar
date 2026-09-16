using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Notifications;

namespace Athkar.DataAccess.Config;

public class BroadcastConfig : IEntityTypeConfiguration<Broadcast>
{
    public void Configure(EntityTypeBuilder<Broadcast> builder)
    {
        builder.Property(x => x.TargetLanguageCode).HasMaxLength(8);
        builder.Property(x => x.TargetDeviceKey).HasMaxLength(64);
        builder.Property(x => x.Route).HasMaxLength(200);
        builder.Property(x => x.AndroidChannelId).HasMaxLength(64).IsRequired();

        builder.HasMany(x => x.Translations).WithOne(x => x.Broadcast)
            .HasForeignKey(x => x.BroadcastId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
    }
}

public class BroadcastTranslationConfig : IEntityTypeConfiguration<BroadcastTranslation>
{
    public void Configure(EntityTypeBuilder<BroadcastTranslation> builder)
    {
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(1000).IsRequired();

        builder.HasIndex(x => new { x.BroadcastId, x.LanguageCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
