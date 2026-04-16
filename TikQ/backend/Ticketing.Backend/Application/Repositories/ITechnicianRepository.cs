using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ITechnicianRepository
{
    Task<IEnumerable<Technician>> GetAllAsync();
    Task<IEnumerable<Technician>> GetAllIncludingDeletedAsync(); // Bypasses soft delete filter
    Task<IEnumerable<Technician>> GetActiveWithUserIdAsync();
    Task<Technician?> GetByIdAsync(Guid id);
    Task<Technician?> GetByIdIncludingDeletedAsync(Guid id); // Bypasses soft delete filter
    Task<Technician?> GetByIdWithIncludesAsync(Guid id); // Includes User, Permissions
    Task<Technician?> GetByUserIdAsync(Guid userId); // Get technician by linked User.Id
    Task<Technician> AddAsync(Technician technician);
    Task UpdateAsync(Technician technician);
    Task<IEnumerable<Guid>> GetTechnicianUserIdsBySubcategoryAsync(int subcategoryId);
}