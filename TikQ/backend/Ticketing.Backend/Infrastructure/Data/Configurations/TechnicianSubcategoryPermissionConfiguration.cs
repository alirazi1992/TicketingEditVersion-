using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class TechnicianSubcategoryPermissionConfiguration : IEntityTypeConfiguration<TechnicianSubcategoryPermission>
{
    public void Configure(EntityTypeBuilder<TechnicianSubcategoryPermission> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        // Foreign key to Technician
        builder.HasOne(p => p.Technician)
            .WithMany(t => t.SubcategoryPermissions)
            .HasForeignKey(p => p.TechnicianId)
            .OnDelete(DeleteBehavior.Cascade);

        // Foreign key to Subcategory
        builder.HasOne(p => p.Subcategory)
            .WithMany()
            .HasForeignKey(p => p.SubcategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint: a technician cannot have duplicate permissions for the same subcategory
        builder.HasIndex(p => new { p.TechnicianId, p.SubcategoryId })
            .IsUnique();

        // Index for querying technicians by subcategory
        builder.HasIndex(p => p.SubcategoryId);
    }
}

