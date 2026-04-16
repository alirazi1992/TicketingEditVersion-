using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ISupervisorTechnicianLinkRepository
{
    Task<IEnumerable<SupervisorTechnicianLink>> GetLinksForSupervisorAsync(Guid supervisorUserId);
    Task<IEnumerable<SupervisorTechnicianLink>> GetLinksForTechnicianAsync(Guid technicianUserId);
    Task<bool> IsLinkedAsync(Guid supervisorUserId, Guid technicianUserId);
    Task<SupervisorTechnicianLink> AddAsync(SupervisorTechnicianLink link);
    Task<bool> RemoveAsync(Guid supervisorUserId, Guid technicianUserId);
    Task<int> GetTotalCountAsync();
    Task<int> GetCountForSupervisorAsync(Guid supervisorUserId);
}

