using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(int id);
    Task<Category?> GetByIdWithSubcategoriesAsync(int id);
    Task<Category?> GetByIdWithTicketsAndSubcategoriesAsync(int id);
    Task<Subcategory?> GetSubcategoryByIdAsync(int id);
    Task<Subcategory?> GetSubcategoryByIdWithTicketsAsync(int id);
    Task<IEnumerable<Category>> GetAllAsync();
    Task<IEnumerable<Category>> GetActiveCategoriesAsync();
    Task<IEnumerable<Category>> SearchAsync(string? search, int skip, int take);
    Task<int> CountAsync(string? search = null);
    Task<bool> ExistsByNameAsync(string name);
    Task<bool> ExistsByNameExcludingIdAsync(string name, int excludeId);
    Task<bool> SubcategoryExistsByNameAsync(int categoryId, string name);
    Task<bool> SubcategoryExistsByNameExcludingIdAsync(int categoryId, string name, int excludeId);
    Task<IEnumerable<Subcategory>> GetSubcategoriesByCategoryIdAsync(int categoryId);
    /// <summary>Returns the next available Id for a new category (max+1). Use inside a transaction to avoid races.</summary>
    Task<int> GetNextCategoryIdAsync();
    /// <summary>Returns the next available Id for a new subcategory (max+1). Use inside a transaction to avoid races.</summary>
    Task<int> GetNextSubcategoryIdAsync();
    Task<Category> AddAsync(Category category);
    Task<Subcategory> AddSubcategoryAsync(Subcategory subcategory);
    Task UpdateAsync(Category category);
    Task UpdateSubcategoryAsync(Subcategory subcategory);
    Task<bool> DeleteAsync(int id);
    Task<bool> DeleteSubcategoryAsync(int id);
}


