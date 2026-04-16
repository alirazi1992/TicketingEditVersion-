using Ticketing.Domain.Entities;

namespace Ticketing.Application.Repositories;

public interface ITechnicianRepository
{
    Task<Technician?> GetByIdAsync(Guid id);
    Task<Technician?> GetByIdWithIncludesAsync(Guid id); // Includes User, Permissions
    Task<IEnumerable<Technician>> GetAllAsync();
    Task<IEnumerable<Technician>> GetActiveAsync();
    Task<Technician?> GetByUserIdAsync(Guid userId);
    Task<Technician> AddAsync(Technician technician);
    Task<Technician> UpdateAsync(Technician technician);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> HasAssignedTicketsAsync(Guid technicianId);
    Task<IEnumerable<Guid>> GetTechnicianUserIdsBySubcategoryAsync(int subcategoryId);
    Task UpdateSubcategoryPermissionsAsync(Guid technicianId, List<int> subcategoryIds);
}
