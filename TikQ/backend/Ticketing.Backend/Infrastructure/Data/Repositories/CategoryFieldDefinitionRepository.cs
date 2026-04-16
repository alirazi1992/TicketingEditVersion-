using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Infrastructure.Data.Repositories;

public class CategoryFieldDefinitionRepository : ICategoryFieldDefinitionRepository
{
    private readonly AppDbContext _context;

    public CategoryFieldDefinitionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CategoryFieldDefinition?> GetByIdAsync(int id)
    {
        return await _context.CategoryFieldDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<IEnumerable<CategoryFieldDefinition>> GetByCategoryIdAsync(int categoryId, bool includeInactive = true)
    {
        var query = _context.CategoryFieldDefinitions
            .AsNoTracking()
            .Where(f => f.CategoryId == categoryId);

        if (!includeInactive)
        {
            query = query.Where(f => f.IsActive);
        }

        return await query
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Id)
            .ToListAsync();
    }

    public async Task<CategoryFieldDefinition> AddAsync(CategoryFieldDefinition fieldDefinition)
    {
        await _context.CategoryFieldDefinitions.AddAsync(fieldDefinition);
        return fieldDefinition;
    }

    public Task<CategoryFieldDefinition> UpdateAsync(CategoryFieldDefinition fieldDefinition)
    {
        _context.CategoryFieldDefinitions.Update(fieldDefinition);
        return Task.FromResult(fieldDefinition);
    }

    public async Task<bool> ExistsAsync(int categoryId, string key)
    {
        return await _context.CategoryFieldDefinitions
            .AsNoTracking()
            .AnyAsync(f => f.CategoryId == categoryId && f.Key == key);
    }
}















