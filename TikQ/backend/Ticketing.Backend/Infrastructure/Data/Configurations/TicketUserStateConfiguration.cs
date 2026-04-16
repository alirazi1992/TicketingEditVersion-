using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class TicketUserStateConfiguration : IEntityTypeConfiguration<TicketUserState>
{
    public void Configure(EntityTypeBuilder<TicketUserState> builder)
    {
        builder.ToTable("TicketUserStates");

        builder.HasKey(tus => tus.Id);

        builder.HasIndex(tus => new { tus.TicketId, tus.UserId })
            .IsUnique();

        builder.Property(tus => tus.LastSeenAt)
            .IsRequired(false);

        builder.HasOne(tus => tus.Ticket)
            .WithMany()
            .HasForeignKey(tus => tus.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tus => tus.User)
            .WithMany()
            .HasForeignKey(tus => tus.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

