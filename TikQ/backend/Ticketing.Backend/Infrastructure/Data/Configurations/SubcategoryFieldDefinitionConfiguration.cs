using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class SubcategoryFieldDefinitionConfiguration : IEntityTypeConfiguration<SubcategoryFieldDefinition>
{
    public void Configure(EntityTypeBuilder<SubcategoryFieldDefinition> builder)
    {
        builder.ToTable("SubcategoryFieldDefinitions");
        
        builder.HasKey(f => f.Id);
        // SQL Server TikQ.dbo.SubcategoryFieldDefinitions.Id is not IDENTITY; we set Id in code.
        builder.Property(f => f.Id).ValueGeneratedNever();
        
        builder.Property(f => f.SubcategoryId).IsRequired();
        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Label).IsRequired().HasMaxLength(200);
        builder.Property(f => f.FieldKey).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Type).IsRequired().HasConversion<string>();
        builder.Property(f => f.IsRequired).IsRequired().HasDefaultValue(false);
        builder.Property(f => f.DefaultValue).HasMaxLength(500);
        builder.Property(f => f.OptionsJson).HasColumnType("TEXT");
        builder.Property(f => f.Min).HasColumnType("REAL");
        builder.Property(f => f.Max).HasColumnType("REAL");
        builder.Property(f => f.SortOrder).HasDefaultValue(0);
        builder.Property(f => f.IsActive).HasDefaultValue(true);
        builder.Property(f => f.CreatedAt).IsRequired();
        
        // Relationship
        builder.HasOne(f => f.Subcategory)
            .WithMany()
            .HasForeignKey(f => f.SubcategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Unique constraint: Key must be unique per subcategory
        builder.HasIndex(f => new { f.SubcategoryId, f.FieldKey }).IsUnique();
        
        // Index for efficient queries
        builder.HasIndex(f => f.SubcategoryId);
    }
}
