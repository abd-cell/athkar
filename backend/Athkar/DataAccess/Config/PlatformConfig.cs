using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Audit;
using Athkar.Areas.Domain.Configuration;
using Athkar.Areas.Domain.Logging;

namespace Athkar.DataAccess.Config;

public class AppConfigurationConfig : IEntityTypeConfiguration<AppConfiguration>
{
    public void Configure(EntityTypeBuilder<AppConfiguration> builder)
    {
        builder.Property(x => x.PrimaryColor).HasMaxLength(9).IsRequired();
        builder.Property(x => x.ReviewingScholar).HasMaxLength(200);
        builder.Property(x => x.ReviewingScholarCredential).HasMaxLength(500);
        builder.Property(x => x.SupportEmail).HasMaxLength(256);
        builder.Property(x => x.SupportWebsite).HasMaxLength(500);
        builder.Property(x => x.PrivacyPolicyUrl).HasMaxLength(500);
        builder.Property(x => x.AndroidPackageName).HasMaxLength(200);
        builder.Property(x => x.IosAppStoreId).HasMaxLength(32);
    }
}

public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(x => x.Action).HasMaxLength(128).IsRequired();
        builder.Property(x => x.EntityName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);

        builder.HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.CreationDate);
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
    }
}

public class ApiLogConfig : IEntityTypeConfiguration<ApiLog>
{
    public void Configure(EntityTypeBuilder<ApiLog> builder)
    {
        builder.Property(x => x.Method).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(512).IsRequired();
        builder.Property(x => x.QueryString).HasMaxLength(1024);
        builder.Property(x => x.DeviceKey).HasMaxLength(64);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(400);

        builder.HasIndex(x => x.CreationDate);
        builder.HasIndex(x => x.StatusCode);
    }
}
