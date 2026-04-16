using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data.Configurations;

public class TicketFieldValueConfiguration : IEntityTypeConfiguration<TicketFieldValue>
{
    public void Configure(EntityTypeBuilder<TicketFieldValue> builder)
    {
        builder.ToTable("TicketFieldValues");
        
        builder.HasKey(tfv => tfv.Id);
        builder.Property(tfv => tfv.Id).ValueGeneratedOnAdd();
        
        builder.Property(tfv => tfv.TicketId).IsRequired();
        builder.Property(tfv => tfv.FieldDefinitionId).IsRequired();
        builder.Property(tfv => tfv.Value).IsRequired().HasMaxLength(2000);
        builder.Property(tfv => tfv.CreatedAt).IsRequired();
        
        // Relationships
        builder.HasOne(tfv => tfv.Ticket)
            .WithMany(t => t.FieldValues)
            .HasForeignKey(tfv => tfv.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(tfv => tfv.FieldDefinition)
            .WithMany()
            .HasForeignKey(tfv => tfv.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Indexes
        builder.HasIndex(tfv => tfv.TicketId);
        builder.HasIndex(tfv => tfv.FieldDefinitionId);
        builder.HasIndex(tfv => new { tfv.TicketId, tfv.FieldDefinitionId }).IsUnique();
    }
}


































