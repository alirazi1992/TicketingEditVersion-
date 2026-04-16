using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ITechnicianSubcategoryPermissionRepository
{
    Task<IEnumerable<TechnicianSubcategoryPermission>> GetByTechnicianIdAsync(Guid technicianId);
    Task<IEnumerable<Guid>> GetTechnicianUserIdsBySubcategoryIdAsync(int subcategoryId);
    Task<IEnumerable<Guid>> GetTechnicianUserIdsByCategoryIdAsync(int categoryId);
    Task ReplacePermissionsAsync(Guid technicianId, IEnumerable<int> subcategoryIds);
    Task<TechnicianSubcategoryPermission> AddAsync(TechnicianSubcategoryPermission permission);
    Task DeleteAsync(Guid permissionId);
    Task DeleteByTechnicianIdAsync(Guid technicianId);
}

