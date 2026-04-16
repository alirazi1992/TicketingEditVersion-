using Ticketing.Domain.Entities;

namespace Ticketing.Application.Repositories;

public interface IFieldDefinitionRepository
{
    Task<SubcategoryFieldDefinition?> GetByIdAsync(int id);
    Task<IEnumerable<SubcategoryFieldDefinition>> GetBySubcategoryIdAsync(int subcategoryId, bool includeInactive = true);
    Task<SubcategoryFieldDefinition> AddAsync(SubcategoryFieldDefinition fieldDefinition);
    Task<SubcategoryFieldDefinition> UpdateAsync(SubcategoryFieldDefinition fieldDefinition);
    Task<bool> DeleteAsync(int id);
}
