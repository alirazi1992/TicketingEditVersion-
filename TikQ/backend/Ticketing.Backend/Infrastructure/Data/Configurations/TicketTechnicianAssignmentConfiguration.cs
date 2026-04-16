using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class TicketTechnicianAssignmentConfiguration : IEntityTypeConfiguration<TicketTechnicianAssignment>
{
    public void Configure(EntityTypeBuilder<TicketTechnicianAssignment> builder)
    {
        builder.ToTable("TicketTechnicianAssignments");

        builder.HasKey(ta => ta.Id);
        builder.Property(ta => ta.Id).ValueGeneratedOnAdd();

        builder.Property(ta => ta.TicketId).IsRequired();
        builder.Property(ta => ta.TechnicianUserId).IsRequired();
        builder.Property(ta => ta.AssignedAt).IsRequired();
        builder.Property(ta => ta.AssignedByUserId).IsRequired();
        builder.Property(ta => ta.AcceptedAt);
        builder.Property(ta => ta.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(ta => ta.Role).HasMaxLength(50);

        // Foreign keys
        builder.HasOne(ta => ta.Ticket)
            .WithMany(t => t.AssignedTechnicians)
            .HasForeignKey(ta => ta.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ta => ta.TechnicianUser)
            .WithMany()
            .HasForeignKey(ta => ta.TechnicianUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ta => ta.AssignedByUser)
            .WithMany()
            .HasForeignKey(ta => ta.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ta => ta.Technician)
            .WithMany()
            .HasForeignKey(ta => ta.TechnicianId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(ta => ta.TicketId);
        builder.HasIndex(ta => ta.TechnicianUserId);
        builder.HasIndex(ta => new { ta.TicketId, ta.TechnicianUserId, ta.IsActive });
    }
}


































