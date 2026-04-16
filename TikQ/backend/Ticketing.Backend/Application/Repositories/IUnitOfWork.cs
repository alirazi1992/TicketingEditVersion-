using Microsoft.EntityFrameworkCore.Storage;

namespace Ticketing.Backend.Application.Repositories;

/// <summary>
/// Unit of Work pattern to coordinate multiple repository operations
/// and ensure a single transaction boundary for SaveChanges operations
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Starts a database transaction. Use to make GetNextCategoryId + Add + SaveChanges atomic.</summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    /// <summary>Runs the given action inside a transaction and the execution strategy (required when using SqlServerRetryingExecutionStrategy).</summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
    ICategoryFieldDefinitionRepository CategoryFieldDefinitions { get; }
    IFieldDefinitionRepository FieldDefinitions { get; }
    ITicketTechnicianAssignmentRepository TicketTechnicianAssignments { get; }
    ITicketActivityEventRepository TicketActivityEvents { get; }
    ITechnicianSubcategoryPermissionRepository TechnicianSubcategoryPermissions { get; }
    ITicketUserStateRepository TicketUserStates { get; }
    ISupervisorTechnicianLinkRepository SupervisorTechnicianLinks { get; }
    
    /// <summary>
    /// Saves all changes made in this unit of work to the database
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}


