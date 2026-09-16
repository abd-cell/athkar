using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Support;
using Athkar.Shareds.Constants;

namespace Athkar.DataAccess.Config;

public class FaqItemConfig : IEntityTypeConfiguration<FaqItem>
{
    public void Configure(EntityTypeBuilder<FaqItem> builder)
    {
        builder.HasIndex(x => new { x.Category, x.SortOrder });

        builder.HasMany(x => x.Translations).WithOne(x => x.FaqItem)
            .HasForeignKey(x => x.FaqItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class FaqTranslationConfig : IEntityTypeConfiguration<FaqTranslation>
{
    public void Configure(EntityTypeBuilder<FaqTranslation> builder)
    {
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Question).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Answer).HasMaxLength(4000).IsRequired();

        builder.HasIndex(x => new { x.FaqItemId, x.LanguageCode })
            .IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class FeedbackConfig : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.Property(x => x.Message).HasMaxLength(ContentRules.MaxTextLength).IsRequired();
        builder.Property(x => x.ContactEmail).HasMaxLength(256);
        builder.Property(x => x.AppVersion).HasMaxLength(32);
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Reply).HasMaxLength(ContentRules.MaxTextLength);

        builder.HasOne(x => x.Device).WithMany()
            .HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);

        // NoAction, not SetNull: the reviewer who answered a correction is part
        // of the record, and a staff account being deleted must not quietly
        // detach it.
        builder.HasOne(x => x.RepliedByUser).WithMany()
            .HasForeignKey(x => x.RepliedBy).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.Status, x.CreationDate });
        builder.HasIndex(x => x.DeviceId);
    }
}
