using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _context;

    public CategoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Category?> GetByIdAsync(int id)
    {
        return await _context.Categories.FindAsync(id);
    }

    public async Task<Category?> GetByIdWithSubcategoriesAsync(int id)
    {
        return await _context.Categories
            .Include(c => c.Subcategories)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Category?> GetByIdWithTicketsAndSubcategoriesAsync(int id)
    {
        return await _context.Categories
            .Include(c => c.Tickets)
            .Include(c => c.Subcategories)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Subcategory?> GetSubcategoryByIdAsync(int id)
    {
        return await _context.Subcategories.FindAsync(id);
    }

    public async Task<Subcategory?> GetSubcategoryByIdWithTicketsAsync(int id)
    {
        return await _context.Subcategories
            .Include(s => s.Tickets)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Category>> GetAllAsync()
    {
        return await _context.Categories
            .Include(c => c.Subcategories)
            .ToListAsync();
    }

    public async Task<IEnumerable<Category>> GetActiveCategoriesAsync()
    {
        // Include subcategories so GET /api/categories returns non-empty Subcategories (lazy-loading is off)
        return await _context.Categories
            .Where(c => c.IsActive)
            .Include(c => c.Subcategories.Where(sc => sc.IsActive))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Category>> SearchAsync(string? search, int skip, int take)
    {
        var query = _context.Categories.Include(c => c.Subcategories).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || (c.Description != null && c.Description.Contains(search)));
        }

        return await query
            .OrderBy(c => c.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> CountAsync(string? search = null)
    {
        var query = _context.Categories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search) || (c.Description != null && c.Description.Contains(search)));
        }

        return await query.CountAsync();
    }

    public async Task<bool> ExistsByNameAsync(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return await _context.Categories.AnyAsync(c => c.NormalizedName == normalized);
    }

    public async Task<bool> ExistsByNameExcludingIdAsync(string name, int excludeId)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return await _context.Categories.AnyAsync(c => c.NormalizedName == normalized && c.Id != excludeId);
    }

    public async Task<bool> SubcategoryExistsByNameAsync(int categoryId, string name)
    {
        return await _context.Subcategories.AnyAsync(s => s.CategoryId == categoryId && s.Name == name);
    }

    public async Task<bool> SubcategoryExistsByNameExcludingIdAsync(int categoryId, string name, int excludeId)
    {
        return await _context.Subcategories.AnyAsync(s => s.CategoryId == categoryId && s.Name == name && s.Id != excludeId);
    }

    public async Task<IEnumerable<Subcategory>> GetSubcategoriesByCategoryIdAsync(int categoryId)
    {
        return await _context.Subcategories
            .Where(s => s.CategoryId == categoryId)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<int> GetNextCategoryIdAsync()
    {
        var max = await _context.Categories.MaxAsync(c => (int?)c.Id);
        return (max ?? 0) + 1;
    }

    public async Task<int> GetNextSubcategoryIdAsync()
    {
        var max = await _context.Subcategories.MaxAsync(s => (int?)s.Id);
        return (max ?? 0) + 1;
    }

    public async Task<Category> AddAsync(Category category)
    {
        await _context.Categories.AddAsync(category);
        return category;
    }

    public async Task<Subcategory> AddSubcategoryAsync(Subcategory subcategory)
    {
        await _context.Subcategories.AddAsync(subcategory);
        return subcategory;
    }

    public Task UpdateAsync(Category category)
    {
        _context.Categories.Update(category);
        return Task.CompletedTask;
    }

    public Task UpdateSubcategoryAsync(Subcategory subcategory)
    {
        _context.Subcategories.Update(subcategory);
        return Task.CompletedTask;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category = await GetByIdWithTicketsAndSubcategoriesAsync(id);
        if (category == null)
        {
            return false;
        }

        _context.Categories.Remove(category);
        return true;
    }

    public async Task<bool> DeleteSubcategoryAsync(int id)
    {
        var subcategory = await GetSubcategoryByIdWithTicketsAsync(id);
        if (subcategory == null)
        {
            return false;
        }

        _context.Subcategories.Remove(subcategory);
        return true;
    }
}


