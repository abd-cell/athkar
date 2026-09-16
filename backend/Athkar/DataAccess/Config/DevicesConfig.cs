using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Devices;

namespace Athkar.DataAccess.Config;

public class DeviceConfig : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.Property(x => x.DeviceKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PushToken).HasMaxLength(512);
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AppVersion).HasMaxLength(32);
        builder.Property(x => x.CountryCode).HasMaxLength(2);

        builder.HasIndex(x => x.DeviceKey).IsUnique();

        // The audience query for every fan-out: enabled devices holding a token,
        // narrowed by language. Covering it here is the difference between a
        // broadcast taking seconds and taking minutes.
        builder.HasIndex(x => new { x.NotificationsEnabled, x.LanguageCode })
            .HasFilter("[PushToken] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasIndex(x => x.LastSeenAt);
    }
}

public class DeviceNotificationConfig : IEntityTypeConfiguration<DeviceNotification>
{
    public void Configure(EntityTypeBuilder<DeviceNotification> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Route).HasMaxLength(200);

        builder.HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);

        // The inbox is always read newest-first for one device.
        builder.HasIndex(x => new { x.DeviceId, x.CreationDate });
    }
}
