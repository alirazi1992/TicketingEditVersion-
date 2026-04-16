using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class SubcategoryConfiguration : IEntityTypeConfiguration<Subcategory>
{
    public void Configure(EntityTypeBuilder<Subcategory> builder)
    {
        builder.HasKey(sc => sc.Id);
        // Do not use ValueGeneratedOnAdd: SQL Server Subcategories.Id may not be IDENTITY; we set Id in code.
        builder.Property(sc => sc.Id).ValueGeneratedNever();
        builder.Property(sc => sc.Name).IsRequired().HasMaxLength(200);

        builder.HasOne(sc => sc.Category)
            .WithMany(c => c.Subcategories)
            .HasForeignKey(sc => sc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
