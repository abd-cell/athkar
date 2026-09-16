using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Athkar.Areas.Domain.Staff;

namespace Athkar.DataAccess.Config;

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.LanguageCode).HasMaxLength(8).IsRequired();

        // Filtered, so a deleted account frees its address for re-use while a
        // live duplicate is still impossible.
        builder.HasIndex(x => x.Email).IsUnique().HasFilter("[IsDeleted] = 0");

        builder.HasMany(x => x.Roles).WithOne(x => x.User)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Logins).WithOne(x => x.User)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserRoleConfig : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.Role }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class UserLoginConfig : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> builder)
    {
        builder.Property(x => x.SessionKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.RefreshToken).HasMaxLength(128).IsRequired();
        builder.Property(x => x.UserAgent).HasMaxLength(400);
        builder.Property(x => x.IpAddress).HasMaxLength(64);

        // Looked up on every single authenticated request, so it is indexed even
        // though the table is small.
        builder.HasIndex(x => x.SessionKey);
        builder.HasIndex(x => x.RefreshToken);
    }
}
