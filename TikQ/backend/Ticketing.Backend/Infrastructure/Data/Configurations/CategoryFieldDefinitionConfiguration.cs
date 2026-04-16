using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class CategoryFieldDefinitionConfiguration : IEntityTypeConfiguration<CategoryFieldDefinition>
{
    public void Configure(EntityTypeBuilder<CategoryFieldDefinition> builder)
    {
        builder.ToTable("CategoryFieldDefinitions");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedOnAdd();

        builder.Property(f => f.CategoryId).IsRequired();
        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Label).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Key).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Type).IsRequired().HasConversion<string>();
        builder.Property(f => f.IsRequired).IsRequired().HasDefaultValue(false);
        builder.Property(f => f.DefaultValue).HasMaxLength(500);
        builder.Property(f => f.OptionsJson);
        builder.Property(f => f.SortOrder).HasDefaultValue(0);
        builder.Property(f => f.IsActive).HasDefaultValue(true);
        builder.Property(f => f.CreatedAt).IsRequired();

        builder.HasOne(f => f.Category)
            .WithMany()
            .HasForeignKey(f => f.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.CategoryId);
        builder.HasIndex(f => new { f.CategoryId, f.Key }).IsUnique();
    }
}















