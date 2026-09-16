using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Reminders;

namespace Athkar.DataAccess.Config;

public class ReminderCampaignConfig : IEntityTypeConfiguration<ReminderCampaign>
{
    public void Configure(EntityTypeBuilder<ReminderCampaign> builder)
    {
        builder.Property(x => x.Key).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TargetLanguageCode).HasMaxLength(8);
        builder.Property(x => x.AndroidChannelId).HasMaxLength(64).IsRequired();

        builder.HasIndex(x => x.Key).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Translations).WithOne(x => x.Campaign)
            .HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReminderCampaignTranslationConfig : IEntityTypeConfiguration<ReminderCampaignTranslation>
{
    public void Configure(EntityTypeBuilder<ReminderCampaignTranslation> builder)
    {
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(1000).IsRequired();

        builder.HasIndex(x => new { x.CampaignId, x.LanguageCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PushDispatchConfig : IEntityTypeConfiguration<PushDispatch>
{
    public void Configure(EntityTypeBuilder<PushDispatch> builder)
    {
        builder.Property(x => x.MessageId).HasMaxLength(256);
        builder.Property(x => x.Error).HasMaxLength(1000);

        builder.HasOne(x => x.Campaign).WithMany()
            .HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Broadcast).WithMany()
            .HasForeignKey(x => x.BroadcastId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);

        // The idempotency key for the whole pipeline: the materialiser can run
        // twice, or overlap itself after a slow pass, and still never queue the
        // same reminder to the same device for the same instant.
        builder.HasIndex(x => new { x.CampaignId, x.DeviceId, x.ScheduledAtUtc })
            .IsUnique()
            .HasFilter("[CampaignId] IS NOT NULL");

        // The sender's own query: what is due and still pending.
        builder.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
    }
}
