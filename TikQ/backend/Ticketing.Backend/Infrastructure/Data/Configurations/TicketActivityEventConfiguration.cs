using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class TicketActivityEventConfiguration : IEntityTypeConfiguration<TicketActivityEvent>
{
    public void Configure(EntityTypeBuilder<TicketActivityEvent> builder)
    {
        builder.ToTable("TicketActivityEvents");

        builder.HasKey(tae => tae.Id);
        builder.Property(tae => tae.Id).ValueGeneratedOnAdd();

        builder.Property(tae => tae.TicketId).IsRequired();
        builder.Property(tae => tae.ActorUserId).IsRequired();
        builder.Property(tae => tae.ActorRole).IsRequired().HasMaxLength(50);
        builder.Property(tae => tae.EventType).IsRequired().HasMaxLength(100);
        builder.Property(tae => tae.OldStatus).HasMaxLength(50);
        builder.Property(tae => tae.NewStatus).HasMaxLength(50);
        builder.Property(tae => tae.MetadataJson).HasMaxLength(2000);
        builder.Property(tae => tae.CreatedAt).IsRequired();

        // Foreign keys
        builder.HasOne(tae => tae.Ticket)
            .WithMany(t => t.ActivityEvents)
            .HasForeignKey(tae => tae.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tae => tae.ActorUser)
            .WithMany()
            .HasForeignKey(tae => tae.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(tae => tae.TicketId);
        builder.HasIndex(tae => tae.CreatedAt);
        builder.HasIndex(tae => new { tae.TicketId, tae.CreatedAt });
    }
}


































