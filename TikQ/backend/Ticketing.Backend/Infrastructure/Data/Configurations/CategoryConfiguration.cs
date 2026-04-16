using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        // Do not use ValueGeneratedOnAdd: SQL Server TikQ.dbo.Categories.Id is not IDENTITY, so we set Id in code and must include it in INSERT.
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NormalizedName).IsRequired().HasMaxLength(200);
        builder.HasIndex(c => c.NormalizedName).IsUnique();
    }
}
