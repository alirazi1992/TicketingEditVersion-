using System;
using Microsoft.Extensions.Logging;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;

namespace Ticketing.Backend.Application.Services;

public interface ICategoryService
{
    Task<IEnumerable<CategoryResponse>> GetAllAsync();
    Task<CategoryListResponse> GetAdminCategoriesAsync(string? search = null, int page = 1, int pageSize = 50);
    Task<CategoryResponse?> CreateAsync(CategoryRequest request, IEnumerable<SubcategoryRequest>? subcategories = null);
    Task<CategoryResponse?> UpdateAsync(int id, CategoryRequest request);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<SubcategoryResponse>> GetSubcategoriesAsync(int categoryId);
    Task<SubcategoryResponse?> CreateSubcategoryAsync(int categoryId, SubcategoryRequest request);
    Task<SubcategoryResponse?> UpdateSubcategoryAsync(int id, SubcategoryRequest request);
    Task<bool> DeleteSubcategoryAsync(int id);
}

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CategoryService>? _logger;

    public CategoryService(ICategoryRepository repository, IUnitOfWork unitOfWork, ILogger<CategoryService>? logger = null)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<CategoryResponse>> GetAllAsync()
    {
        // Public endpoint - only return active categories
        var categories = await _repository.GetActiveCategoriesAsync();
        return categories.Select(c => new CategoryResponse
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
            Subcategories = c.Subcategories
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name) // Sort by name for consistent ordering
                .Select((s, index) => MapSubcategoryToResponse(s, c.Id, index))
        });
    }

    public async Task<CategoryListResponse> GetAdminCategoriesAsync(string? search = null, int page = 1, int pageSize = 50)
    {
        var skip = (page - 1) * pageSize;
        var totalCount = await _repository.CountAsync(search);
        var items = await _repository.SearchAsync(search, skip, pageSize);

        return new CategoryListResponse
        {
            Items = items.Select(MapToResponse),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CategoryResponse?> CreateAsync(CategoryRequest request, IEnumerable<SubcategoryRequest>? subcategories = null)
    {
        _logger?.LogInformation("CategoryService.CreateAsync: Starting - Name={Name}, IsActive={IsActive}", request.Name, request.IsActive);
        
        // Check for duplicate name
        var exists = await _repository.ExistsByNameAsync(request.Name);
        if (exists)
        {
            _logger?.LogWarning("CategoryService.CreateAsync: Duplicate name - Name={Name}", request.Name);
            throw new InvalidOperationException($"Category with name '{request.Name}' already exists");
        }

        var category = new Category
        {
            Name = request.Name,
            NormalizedName = NormalizeName(request.Name),
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            Subcategories = subcategories?.Select(sc => new Subcategory 
            { 
                Name = sc.Name,
                Description = sc.Description,
                IsActive = sc.IsActive,
                CreatedAt = DateTime.UtcNow
            }).ToList() ?? new List<Subcategory>()
        };

        // When Categories.Id is not IDENTITY, we set Id explicitly. Use a transaction inside the execution strategy
        // so it works with SqlServerRetryingExecutionStrategy (EnableRetryOnFailure).
        int savedCount = 0;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var nextId = await _repository.GetNextCategoryIdAsync();
            category.Id = nextId;
            _logger?.LogInformation("CategoryService.CreateAsync: Adding to repository - Id={Id}, Name={Name}", category.Id, category.Name);
            await _repository.AddAsync(category);
            _logger?.LogInformation("CategoryService.CreateAsync: Calling SaveChangesAsync");
            savedCount = await _unitOfWork.SaveChangesAsync();
        });
        _logger?.LogInformation("CategoryService.CreateAsync: SaveChangesAsync returned {Count} changes, Category.Id={Id}", savedCount, category.Id);

        // Verify the category was actually saved by fetching it back
        var verifyCategory = await _repository.GetByIdAsync(category.Id);
        if (verifyCategory == null)
        {
            _logger?.LogError("CategoryService.CreateAsync: CRITICAL - Category not found after SaveChangesAsync. Save may have failed silently.");
            throw new InvalidOperationException("Failed to verify saved category - entity not found in database");
        }
        
        _logger?.LogInformation("CategoryService.CreateAsync: VERIFIED - Category saved successfully. Id={Id}, Name={Name}", verifyCategory.Id, verifyCategory.Name);

        return MapToResponse(category);
    }

    public async Task<CategoryResponse?> UpdateAsync(int id, CategoryRequest request)
    {
        var category = await _repository.GetByIdWithSubcategoriesAsync(id);
        if (category == null)
        {
            return null;
        }

        // Check for duplicate name (excluding current category)
        var exists = await _repository.ExistsByNameExcludingIdAsync(request.Name, id);
        if (exists)
        {
            throw new InvalidOperationException($"Category with name '{request.Name}' already exists");
        }

        category.Name = request.Name;
        category.NormalizedName = NormalizeName(request.Name);
        category.Description = request.Description;
        category.IsActive = request.IsActive;
        await _repository.UpdateAsync(category);
        await _unitOfWork.SaveChangesAsync();
        return MapToResponse(category);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category = await _repository.GetByIdWithTicketsAndSubcategoriesAsync(id);
        if (category == null)
        {
            return false;
        }

        // Check if category is used by tickets
        if (category.Tickets.Any())
        {
            throw new InvalidOperationException("Cannot delete category that is used by tickets. Consider deactivating it instead.");
        }

        // Check if category has subcategories
        if (category.Subcategories.Any())
        {
            throw new InvalidOperationException("Cannot delete category that has subcategories. Please delete subcategories first.");
        }

        var deleted = await _repository.DeleteAsync(id);
        if (deleted)
        {
            await _unitOfWork.SaveChangesAsync();
        }
        return true;
    }

    public async Task<IEnumerable<SubcategoryResponse>> GetSubcategoriesAsync(int categoryId)
    {
        var subcategories = await _repository.GetSubcategoriesByCategoryIdAsync(categoryId);
        return subcategories
            .OrderBy(s => s.Name) // Sort by name for consistent ordering
            .Select((s, index) => MapSubcategoryToResponse(s, categoryId, index));
    }

    public async Task<SubcategoryResponse?> CreateSubcategoryAsync(int categoryId, SubcategoryRequest request)
    {
        _logger?.LogInformation("CategoryService.CreateSubcategoryAsync: Starting - CategoryId={CategoryId}, Name={Name}", categoryId, request.Name);
        
        var category = await _repository.GetByIdAsync(categoryId);
        if (category == null)
        {
            _logger?.LogWarning("CategoryService.CreateSubcategoryAsync: Category not found - CategoryId={CategoryId}", categoryId);
            return null;
        }

        // Check for duplicate name within the category
        var exists = await _repository.SubcategoryExistsByNameAsync(categoryId, request.Name);
        if (exists)
        {
            _logger?.LogWarning("CategoryService.CreateSubcategoryAsync: Duplicate name - CategoryId={CategoryId}, Name={Name}", categoryId, request.Name);
            throw new InvalidOperationException($"Subcategory with name '{request.Name}' already exists in this category");
        }

        var subcategory = new Subcategory
        {
            CategoryId = categoryId,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        int savedCount = 0;
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var nextId = await _repository.GetNextSubcategoryIdAsync();
            subcategory.Id = nextId;
            _logger?.LogInformation("CategoryService.CreateSubcategoryAsync: Adding to repository - Id={Id}, Name={Name}", subcategory.Id, subcategory.Name);
            await _repository.AddSubcategoryAsync(subcategory);
            _logger?.LogInformation("CategoryService.CreateSubcategoryAsync: Calling SaveChangesAsync");
            savedCount = await _unitOfWork.SaveChangesAsync();
        });
        _logger?.LogInformation("CategoryService.CreateSubcategoryAsync: SaveChangesAsync returned {Count} changes, Subcategory.Id={Id}", savedCount, subcategory.Id);

        var verifySubcategory = await _repository.GetSubcategoryByIdAsync(subcategory.Id);
        if (verifySubcategory == null)
        {
            _logger?.LogError("CategoryService.CreateSubcategoryAsync: CRITICAL - Subcategory not found after SaveChangesAsync. Save may have failed silently.");
            throw new InvalidOperationException("Failed to verify saved subcategory - entity not found in database");
        }
        
        _logger?.LogInformation("CategoryService.CreateSubcategoryAsync: VERIFIED - Subcategory saved successfully. Id={Id}, Name={Name}, CategoryId={CategoryId}", 
            verifySubcategory.Id, verifySubcategory.Name, verifySubcategory.CategoryId);

        // Apply category-level field templates to new subcategory (set Id for each; SQL Server has no IDENTITY).
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var categoryFields = await _unitOfWork.CategoryFieldDefinitions.GetByCategoryIdAsync(categoryId, includeInactive: true);
            var nextId = await _unitOfWork.FieldDefinitions.GetNextSubcategoryFieldDefinitionIdAsync();
            foreach (var template in categoryFields.Where(f => f.IsActive))
            {
                if (await _unitOfWork.FieldDefinitions.ExistsAsync(subcategory.Id, template.Key))
                {
                    continue;
                }

                await _unitOfWork.FieldDefinitions.AddAsync(new SubcategoryFieldDefinition
                {
                    Id = nextId++,
                    SubcategoryId = subcategory.Id,
                    Name = template.Name,
                    Label = template.Label,
                    FieldKey = template.Key,
                    Type = template.Type,
                    IsRequired = template.IsRequired,
                    DefaultValue = template.DefaultValue,
                    OptionsJson = template.OptionsJson,
                    Min = template.Min,
                    Max = template.Max,
                    SortOrder = template.SortOrder,
                    IsActive = template.IsActive,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _unitOfWork.SaveChangesAsync();
        });

        // Get all subcategories for this category to determine index
        var allSubcategories = await _repository.GetSubcategoriesByCategoryIdAsync(categoryId);
        var sortedSubcategories = allSubcategories.OrderBy(s => s.Name).ToList();
        var index = sortedSubcategories.FindIndex(s => s.Id == subcategory.Id);
        return MapSubcategoryToResponse(subcategory, categoryId, index >= 0 ? index : 0);
    }

    public async Task<SubcategoryResponse?> UpdateSubcategoryAsync(int id, SubcategoryRequest request)
    {
        var subcategory = await _repository.GetSubcategoryByIdAsync(id);
        if (subcategory == null)
        {
            return null;
        }

        // Check for duplicate name within the same category (excluding current subcategory)
        var exists = await _repository.SubcategoryExistsByNameExcludingIdAsync(subcategory.CategoryId, request.Name, id);
        if (exists)
        {
            throw new InvalidOperationException($"Subcategory with name '{request.Name}' already exists in this category");
        }

        subcategory.Name = request.Name;
        subcategory.Description = request.Description;
        subcategory.IsActive = request.IsActive;
        await _repository.UpdateSubcategoryAsync(subcategory);
        await _unitOfWork.SaveChangesAsync();

        // Get all subcategories for this category to determine index
        var allSubcategories = await _repository.GetSubcategoriesByCategoryIdAsync(subcategory.CategoryId);
        var sortedSubcategories = allSubcategories.OrderBy(s => s.Name).ToList();
        var index = sortedSubcategories.FindIndex(s => s.Id == subcategory.Id);
        return MapSubcategoryToResponse(subcategory, subcategory.CategoryId, index >= 0 ? index : 0);
    }

    public async Task<bool> DeleteSubcategoryAsync(int id)
    {
        var subcategory = await _repository.GetSubcategoryByIdWithTicketsAsync(id);
        if (subcategory == null)
        {
            return false;
        }

        // Check if subcategory is used by tickets
        if (subcategory.Tickets.Any())
        {
            throw new InvalidOperationException("Cannot delete subcategory that is used by tickets. Consider deactivating it instead.");
        }

        var deleted = await _repository.DeleteSubcategoryAsync(id);
        if (deleted)
        {
            await _unitOfWork.SaveChangesAsync();
        }
        return true;
    }

    private static CategoryResponse MapToResponse(Category category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Description = category.Description,
        IsActive = category.IsActive,
        CreatedAt = category.CreatedAt,
        Subcategories = category.Subcategories
            .OrderBy(s => s.Name) // Sort by name for consistent ordering
            .Select((s, index) => MapSubcategoryToResponse(s, category.Id, index))
    };

    private static string NormalizeName(string name)
    {
        // Use ToLowerInvariant to match SQL Server migration backfill (LOWER(Name)); keeps unique index consistent.
        return name.Trim().ToLowerInvariant();
    }

    private static SubcategoryResponse MapSubcategoryToResponse(Subcategory subcategory, int categoryId, int indexWithinCategory = 0) => new()
    {
        Id = subcategory.Id,
        CategoryId = categoryId,
        Name = subcategory.Name,
        Description = subcategory.Description,
        IsActive = subcategory.IsActive,
        CreatedAt = subcategory.CreatedAt,
        SortOrder = indexWithinCategory + 1, // 1-based index for display
        SubcategoryDisplayCode = $"{categoryId}.{indexWithinCategory + 1}"
    };
}
