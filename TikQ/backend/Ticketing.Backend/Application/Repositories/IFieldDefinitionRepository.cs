using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface IFieldDefinitionRepository
{
    Task<SubcategoryFieldDefinition?> GetByIdAsync(int id);
    Task<IEnumerable<SubcategoryFieldDefinition>> GetBySubcategoryIdAsync(int subcategoryId, bool includeInactive = true);
    Task<IEnumerable<SubcategoryFieldDefinition>> GetByCategoryIdAsync(int categoryId, bool includeInactive = true);
    /// <summary>Returns the next available Id for a new SubcategoryFieldDefinition (max+1). Use inside a transaction to avoid races.</summary>
    Task<int> GetNextSubcategoryFieldDefinitionIdAsync();
    Task<SubcategoryFieldDefinition> AddAsync(SubcategoryFieldDefinition fieldDefinition);
    Task<SubcategoryFieldDefinition> UpdateAsync(SubcategoryFieldDefinition fieldDefinition);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int subcategoryId, string key);
    Task<bool> ExistsForCategoryAsync(int categoryId, string key);
}


