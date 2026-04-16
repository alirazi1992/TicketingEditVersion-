using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Role).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(50);
        builder.Property(u => u.Department).HasMaxLength(200);
        builder.Property(u => u.AvatarUrl).HasMaxLength(8192);

        // Lockout fields for account security
        builder.Property(u => u.LockoutEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.LockoutEnd)
            .IsRequired(false);

        builder.Property(u => u.SecurityStamp)
            .HasMaxLength(256)
            .IsRequired(false);
    }
}
