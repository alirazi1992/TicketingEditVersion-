using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private ICategoryFieldDefinitionRepository? _categoryFieldDefinitions;
    private IFieldDefinitionRepository? _fieldDefinitions;
    private ITicketTechnicianAssignmentRepository? _ticketTechnicianAssignments;
    private ITicketActivityEventRepository? _ticketActivityEvents;
    private ITechnicianSubcategoryPermissionRepository? _technicianSubcategoryPermissions;
    private ITicketUserStateRepository? _ticketUserStates;
    private ISupervisorTechnicianLinkRepository? _supervisorTechnicianLinks;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            action,
            async (dbContext, state, ct) =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
                try
                {
                    await state();
                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
                return true;
            },
            null,
            cancellationToken);
    }

    public ICategoryFieldDefinitionRepository CategoryFieldDefinitions
    {
        get
        {
            return _categoryFieldDefinitions ??= new CategoryFieldDefinitionRepository(_context);
        }
    }

    public IFieldDefinitionRepository FieldDefinitions
    {
        get
        {
            return _fieldDefinitions ??= new FieldDefinitionRepository(_context);
        }
    }

    public ITicketTechnicianAssignmentRepository TicketTechnicianAssignments
    {
        get
        {
            return _ticketTechnicianAssignments ??= new TicketTechnicianAssignmentRepository(_context);
        }
    }

    public ITicketActivityEventRepository TicketActivityEvents
    {
        get
        {
            return _ticketActivityEvents ??= new TicketActivityEventRepository(_context);
        }
    }

    public ITechnicianSubcategoryPermissionRepository TechnicianSubcategoryPermissions
    {
        get
        {
            return _technicianSubcategoryPermissions ??= new TechnicianSubcategoryPermissionRepository(_context);
        }
    }

    public ITicketUserStateRepository TicketUserStates
    {
        get
        {
            return _ticketUserStates ??= new TicketUserStateRepository(_context);
        }
    }

    public ISupervisorTechnicianLinkRepository SupervisorTechnicianLinks
    {
        get
        {
            return _supervisorTechnicianLinks ??= new SupervisorTechnicianLinkRepository(_context);
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}


