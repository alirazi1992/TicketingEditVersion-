using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Theme).IsRequired().HasMaxLength(20);
        builder.Property(p => p.FontSize).IsRequired().HasMaxLength(10);
        builder.Property(p => p.Language).IsRequired().HasMaxLength(10);
        builder.Property(p => p.Timezone).IsRequired().HasMaxLength(100);

        builder.HasOne(p => p.User)
            .WithOne(u => u.Preferences)
            .HasForeignKey<UserPreferences>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ensure one preferences per user
        builder.HasIndex(p => p.UserId).IsUnique();
    }
}

