using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class SupervisorTechnicianLinkConfiguration : IEntityTypeConfiguration<SupervisorTechnicianLink>
{
    public void Configure(EntityTypeBuilder<SupervisorTechnicianLink> builder)
    {
        builder.ToTable("SupervisorTechnicianLinks");

        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).ValueGeneratedOnAdd();

        builder.Property(link => link.SupervisorUserId).IsRequired();
        builder.Property(link => link.TechnicianUserId).IsRequired();
        builder.Property(link => link.CreatedAt).IsRequired();

        builder.HasIndex(link => new { link.SupervisorUserId, link.TechnicianUserId })
            .IsUnique();

        builder.HasOne(link => link.SupervisorUser)
            .WithMany()
            .HasForeignKey(link => link.SupervisorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(link => link.TechnicianUser)
            .WithMany()
            .HasForeignKey(link => link.TechnicianUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
