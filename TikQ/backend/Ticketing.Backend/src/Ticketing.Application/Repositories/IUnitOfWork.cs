namespace Ticketing.Application.Repositories;

/// <summary>
/// Unit of Work pattern to coordinate multiple repository operations
/// and ensure a single transaction boundary
/// </summary>
public interface IUnitOfWork
{
    ITicketRepository Tickets { get; }
    IUserRepository Users { get; }
    ICategoryRepository Categories { get; }
    ITechnicianRepository Technicians { get; }
    ITicketActivityRepository TicketActivities { get; }
    ITicketMessageRepository TicketMessages { get; }
    ITicketTechnicianRepository TicketTechnicians { get; }
    ITicketWorkSessionRepository TicketWorkSessions { get; }
    ISystemSettingsRepository SystemSettings { get; }
    IUserPreferencesRepository UserPreferences { get; }
    IFieldDefinitionRepository FieldDefinitions { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
