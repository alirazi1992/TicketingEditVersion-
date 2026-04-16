using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Subcategory> Subcategories => Set<Subcategory>();
    public DbSet<CategoryFieldDefinition> CategoryFieldDefinitions => Set<CategoryFieldDefinition>();
    public DbSet<SubcategoryFieldDefinition> SubcategoryFieldDefinitions => Set<SubcategoryFieldDefinition>();
    public DbSet<TicketFieldValue> TicketFieldValues => Set<TicketFieldValue>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
    public DbSet<Technician> Technicians => Set<Technician>();
    public DbSet<TechnicianSubcategoryPermission> TechnicianSubcategoryPermissions => Set<TechnicianSubcategoryPermission>();
    public DbSet<TicketTechnicianAssignment> TicketTechnicianAssignments => Set<TicketTechnicianAssignment>();
    public DbSet<TicketActivityEvent> TicketActivityEvents => Set<TicketActivityEvent>();
    public DbSet<TicketUserState> TicketUserStates => Set<TicketUserState>();
    public DbSet<SupervisorTechnicianLink> SupervisorTechnicianLinks => Set<SupervisorTechnicianLink>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
