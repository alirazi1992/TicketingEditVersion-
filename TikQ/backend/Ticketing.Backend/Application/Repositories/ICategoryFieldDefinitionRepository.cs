using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ICategoryFieldDefinitionRepository
{
    Task<CategoryFieldDefinition?> GetByIdAsync(int id);
    Task<IEnumerable<CategoryFieldDefinition>> GetByCategoryIdAsync(int categoryId, bool includeInactive = true);
    Task<CategoryFieldDefinition> AddAsync(CategoryFieldDefinition fieldDefinition);
    Task<CategoryFieldDefinition> UpdateAsync(CategoryFieldDefinition fieldDefinition);
    Task<bool> ExistsAsync(int categoryId, string key);
}















