using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Services;

public interface IFieldDefinitionService
{
    Task<IEnumerable<FieldDefinitionResponse>> GetFieldDefinitionsAsync(int subcategoryId, bool includeInactive = false);
    Task<IEnumerable<FieldDefinitionResponse>> GetCategoryFieldDefinitionsAsync(int categoryId, bool includeInactive = false);
    Task<IEnumerable<FieldDefinitionResponse>> GetMergedFieldDefinitionsAsync(int categoryId, int? subcategoryId);
    Task<FieldDefinitionResponse?> GetFieldDefinitionAsync(int id);
    Task<FieldDefinitionResponse?> CreateFieldDefinitionAsync(int subcategoryId, CreateFieldDefinitionRequest request);
    Task<FieldDefinitionResponse?> CreateCategoryFieldDefinitionAsync(int categoryId, CreateFieldDefinitionRequest request);
    Task<FieldDefinitionResponse?> UpdateFieldDefinitionAsync(int id, UpdateFieldDefinitionRequest request);
    Task<bool> DeleteFieldDefinitionAsync(int id);
}

public class FieldDefinitionService : IFieldDefinitionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<FieldDefinitionService> _logger;

    public FieldDefinitionService(
        IUnitOfWork unitOfWork,
        ICategoryRepository categoryRepository,
        ILogger<FieldDefinitionService> logger)
    {
        _unitOfWork = unitOfWork;
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<FieldDefinitionResponse>> GetFieldDefinitionsAsync(int subcategoryId, bool includeInactive = false)
    {
        var fields = await _unitOfWork.FieldDefinitions.GetBySubcategoryIdAsync(subcategoryId, includeInactive);
        return fields.Select(MapToResponse);
    }

    public async Task<IEnumerable<FieldDefinitionResponse>> GetCategoryFieldDefinitionsAsync(int categoryId, bool includeInactive = false)
    {
        var fields = await _unitOfWork.CategoryFieldDefinitions.GetByCategoryIdAsync(categoryId, includeInactive);
        return fields.Select(MapCategoryToResponse);
    }

    public async Task<IEnumerable<FieldDefinitionResponse>> GetMergedFieldDefinitionsAsync(int categoryId, int? subcategoryId)
    {
        var categoryFields = await _unitOfWork.CategoryFieldDefinitions.GetByCategoryIdAsync(categoryId, includeInactive: false);
        var subcategoryFields = subcategoryId.HasValue
            ? await _unitOfWork.FieldDefinitions.GetBySubcategoryIdAsync(subcategoryId.Value, includeInactive: false)
            : new List<SubcategoryFieldDefinition>();

        var subKeys = subcategoryFields.Select(f => f.FieldKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var merged = categoryFields
            .Where(cf => !subKeys.Contains(cf.Key))
            .Select(MapCategoryToResponse)
            .Concat(subcategoryFields.Select(MapToResponse))
            .OrderBy(f => f.DisplayOrder)
            .ThenBy(f => f.Id)
            .ToList();

        return merged;
    }

    public async Task<FieldDefinitionResponse?> GetFieldDefinitionAsync(int id)
    {
        var field = await _unitOfWork.FieldDefinitions.GetByIdAsync(id);
        return field == null ? null : MapToResponse(field);
    }

    public async Task<FieldDefinitionResponse?> CreateFieldDefinitionAsync(int subcategoryId, CreateFieldDefinitionRequest request)
    {
        // Verify subcategory exists
        var subcategory = await _categoryRepository.GetSubcategoryByIdAsync(subcategoryId);
        if (subcategory == null)
        {
            _logger.LogWarning("CreateFieldDefinition: Subcategory {SubcategoryId} not found", subcategoryId);
            return null;
        }

        // Check for duplicate key
        var exists = await _unitOfWork.FieldDefinitions.ExistsAsync(subcategoryId, request.Key);
        if (exists)
        {
            throw new InvalidOperationException($"A field with key '{request.Key}' already exists for this subcategory.");
        }

        // Parse Type enum
        if (!Enum.TryParse<FieldType>(request.Type, ignoreCase: true, out var fieldType))
        {
            throw new InvalidOperationException($"Invalid field type: {request.Type}");
        }

        // Validate Select and MultiSelect types require options
        if ((fieldType == FieldType.Select || fieldType == FieldType.MultiSelect) && (request.Options == null || !request.Options.Any()))
        {
            throw new InvalidOperationException($"{fieldType} field type requires at least one option.");
        }

        // Create field with all properties explicitly set
        // IsRequired is bool (not nullable), so it will always have a value
        // Explicitly set IsRequired to ensure it's never default/uninitialized
        var isRequiredValue = request.IsRequired; // Get value from request
        
        var field = new SubcategoryFieldDefinition
        {
            SubcategoryId = subcategoryId,
            Name = request.Name,
            Label = request.Label,
            FieldKey = request.Key,
            Type = fieldType,
            IsRequired = isRequiredValue, // Explicitly set from request (false or true)
            DefaultValue = request.DefaultValue,
            OptionsJson = request.Options != null ? System.Text.Json.JsonSerializer.Serialize(request.Options) : null,
            Min = request.Min,
            Max = request.Max,
            SortOrder = request.DisplayOrder ?? 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        
        // Double-check IsRequired is set (defensive programming)
        // This ensures the value is never default/uninitialized before saving
        if (field.IsRequired == default(bool) && !isRequiredValue)
        {
            // This shouldn't happen, but ensure it's explicitly false
            field.IsRequired = false;
        }
        
        // Log the field being created for debugging
        _logger.LogDebug("Creating field: Name={Name}, FieldKey={FieldKey}, IsRequired={IsRequired}, Type={Type}", 
            field.Name, field.FieldKey, field.IsRequired, field.Type);

        // SubcategoryFieldDefinitions.Id is not IDENTITY on SQL Server; set Id in a transaction.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var nextId = await _unitOfWork.FieldDefinitions.GetNextSubcategoryFieldDefinitionIdAsync();
            field.Id = nextId;
            await _unitOfWork.FieldDefinitions.AddAsync(field);
            await _unitOfWork.SaveChangesAsync();
        });

        _logger.LogInformation("Created field definition {FieldId} for subcategory {SubcategoryId}", field.Id, subcategoryId);

        return MapToResponse(field);
    }

    public async Task<FieldDefinitionResponse?> CreateCategoryFieldDefinitionAsync(int categoryId, CreateFieldDefinitionRequest request)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category == null)
        {
            _logger.LogWarning("CreateCategoryFieldDefinition: Category {CategoryId} not found", categoryId);
            return null;
        }

        var exists = await _unitOfWork.CategoryFieldDefinitions.ExistsAsync(categoryId, request.Key);
        if (exists)
        {
            throw new InvalidOperationException($"A field with key '{request.Key}' already exists for this category.");
        }

        if (await _unitOfWork.FieldDefinitions.ExistsForCategoryAsync(categoryId, request.Key))
        {
            throw new InvalidOperationException($"A field with key '{request.Key}' already exists in a subcategory for this category.");
        }

        if (!Enum.TryParse<FieldType>(request.Type, ignoreCase: true, out var fieldType))
        {
            throw new InvalidOperationException($"Invalid field type: {request.Type}");
        }

        if ((fieldType == FieldType.Select || fieldType == FieldType.MultiSelect) && (request.Options == null || !request.Options.Any()))
        {
            throw new InvalidOperationException($"{fieldType} field type requires at least one option.");
        }

        var template = new CategoryFieldDefinition
        {
            CategoryId = categoryId,
            Name = request.Name,
            Label = request.Label,
            Key = request.Key,
            Type = fieldType,
            IsRequired = request.IsRequired,
            DefaultValue = request.DefaultValue,
            OptionsJson = request.Options != null ? System.Text.Json.JsonSerializer.Serialize(request.Options) : null,
            Min = request.Min,
            Max = request.Max,
            SortOrder = request.DisplayOrder ?? 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var subcategories = await _categoryRepository.GetSubcategoriesByCategoryIdAsync(categoryId);

        // Category + SubcategoryFieldDefinitions in one transaction; set Id for each subcategory field (SQL Server has no IDENTITY).
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _unitOfWork.CategoryFieldDefinitions.AddAsync(template);
            var nextId = await _unitOfWork.FieldDefinitions.GetNextSubcategoryFieldDefinitionIdAsync();
            foreach (var sub in subcategories)
            {
                if (await _unitOfWork.FieldDefinitions.ExistsAsync(sub.Id, request.Key))
                {
                    throw new InvalidOperationException($"A field with key '{request.Key}' already exists for subcategory '{sub.Name}'.");
                }

                var subField = new SubcategoryFieldDefinition
                {
                    Id = nextId++,
                    SubcategoryId = sub.Id,
                    Name = request.Name,
                    Label = request.Label,
                    FieldKey = request.Key,
                    Type = fieldType,
                    IsRequired = request.IsRequired,
                    DefaultValue = request.DefaultValue,
                    OptionsJson = template.OptionsJson,
                    Min = request.Min,
                    Max = request.Max,
                    SortOrder = template.SortOrder,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.FieldDefinitions.AddAsync(subField);
            }
            await _unitOfWork.SaveChangesAsync();
        });

        return MapCategoryToResponse(template);
    }

    public async Task<FieldDefinitionResponse?> UpdateFieldDefinitionAsync(int id, UpdateFieldDefinitionRequest request)
    {
        var field = await _unitOfWork.FieldDefinitions.GetByIdAsync(id);
        if (field == null)
        {
            var categoryField = await _unitOfWork.CategoryFieldDefinitions.GetByIdAsync(id);
            if (categoryField == null)
            {
                return null;
            }

            var oldKey = categoryField.Key;
            if (request.Key != null && request.Key != categoryField.Key)
            {
                var exists = await _unitOfWork.CategoryFieldDefinitions.ExistsAsync(categoryField.CategoryId, request.Key);
                if (exists)
                {
                    throw new InvalidOperationException($"A field with key '{request.Key}' already exists for this category.");
                }
                if (await _unitOfWork.FieldDefinitions.ExistsForCategoryAsync(categoryField.CategoryId, request.Key))
                {
                    throw new InvalidOperationException($"A field with key '{request.Key}' already exists in a subcategory for this category.");
                }
                categoryField.Key = request.Key;
            }

            if (request.Name != null) categoryField.Name = request.Name;
            if (request.Label != null) categoryField.Label = request.Label;
            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                if (!Enum.TryParse<FieldType>(request.Type, ignoreCase: true, out var fieldType))
                {
                    throw new InvalidOperationException($"Invalid field type: {request.Type}");
                }
                categoryField.Type = fieldType;
            }
            if (request.IsRequired.HasValue) categoryField.IsRequired = request.IsRequired.Value;
            if (request.DefaultValue != null) categoryField.DefaultValue = request.DefaultValue;
            if (request.Options != null) categoryField.OptionsJson = System.Text.Json.JsonSerializer.Serialize(request.Options);
            if (request.Min.HasValue) categoryField.Min = request.Min;
            if (request.Max.HasValue) categoryField.Max = request.Max;
            if (request.DisplayOrder.HasValue) categoryField.SortOrder = request.DisplayOrder.Value;
            if (request.IsActive.HasValue) categoryField.IsActive = request.IsActive.Value;
            categoryField.UpdatedAt = DateTime.UtcNow;

            var subcategories = await _categoryRepository.GetSubcategoriesByCategoryIdAsync(categoryField.CategoryId);
            foreach (var sub in subcategories)
            {
                var subFields = await _unitOfWork.FieldDefinitions.GetBySubcategoryIdAsync(sub.Id, includeInactive: true);
                var subField = subFields.FirstOrDefault(f => f.FieldKey == oldKey);
                if (subField == null) continue;

                subField.Name = categoryField.Name;
                subField.Label = categoryField.Label;
                subField.FieldKey = categoryField.Key;
                subField.Type = categoryField.Type;
                subField.IsRequired = categoryField.IsRequired;
                subField.DefaultValue = categoryField.DefaultValue;
                subField.OptionsJson = categoryField.OptionsJson;
                subField.Min = categoryField.Min;
                subField.Max = categoryField.Max;
                subField.SortOrder = categoryField.SortOrder;
                subField.IsActive = categoryField.IsActive;
                subField.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.FieldDefinitions.UpdateAsync(subField);
            }

            await _unitOfWork.CategoryFieldDefinitions.UpdateAsync(categoryField);
            await _unitOfWork.SaveChangesAsync();
            return MapCategoryToResponse(categoryField);
        }

        // Check for duplicate key (if key is being changed)
        if (request.Key != null && request.Key != field.FieldKey)
        {
            var exists = await _unitOfWork.FieldDefinitions.ExistsAsync(field.SubcategoryId, request.Key);
            if (exists)
            {
                throw new InvalidOperationException($"A field with key '{request.Key}' already exists for this subcategory.");
            }
        }

        // Update fields
        if (request.Name != null) field.Name = request.Name;
        if (request.Label != null) field.Label = request.Label;
        if (request.Key != null) field.FieldKey = request.Key;
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            if (!Enum.TryParse<FieldType>(request.Type, ignoreCase: true, out var fieldType))
            {
                throw new InvalidOperationException($"Invalid field type: {request.Type}");
            }
            field.Type = fieldType;
        }
        if (request.IsRequired.HasValue) field.IsRequired = request.IsRequired.Value;
        if (request.DefaultValue != null) field.DefaultValue = request.DefaultValue;
        if (request.Options != null) field.OptionsJson = System.Text.Json.JsonSerializer.Serialize(request.Options);
        if (request.Min.HasValue) field.Min = request.Min;
        if (request.Max.HasValue) field.Max = request.Max;
        if (request.DisplayOrder.HasValue) field.SortOrder = request.DisplayOrder.Value;
        if (request.IsActive.HasValue) field.IsActive = request.IsActive.Value;
        field.UpdatedAt = DateTime.UtcNow;

        // Validate Select and MultiSelect types require options
        if ((field.Type == FieldType.Select || field.Type == FieldType.MultiSelect) && string.IsNullOrWhiteSpace(field.OptionsJson))
        {
            throw new InvalidOperationException($"{field.Type} field type requires at least one option.");
        }

        await _unitOfWork.FieldDefinitions.UpdateAsync(field);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated field definition {FieldId}", id);

        return MapToResponse(field);
    }

    public async Task<bool> DeleteFieldDefinitionAsync(int id)
    {
        var field = await _unitOfWork.FieldDefinitions.GetByIdAsync(id);
        if (field != null)
        {
            field.IsActive = false;
            field.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.FieldDefinitions.UpdateAsync(field);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Deactivated field definition {FieldId}", id);
            return true;
        }

        var categoryField = await _unitOfWork.CategoryFieldDefinitions.GetByIdAsync(id);
        if (categoryField == null)
        {
            return false;
        }

        categoryField.IsActive = false;
        categoryField.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.CategoryFieldDefinitions.UpdateAsync(categoryField);

        var subcategories = await _categoryRepository.GetSubcategoriesByCategoryIdAsync(categoryField.CategoryId);
        foreach (var sub in subcategories)
        {
            var subFields = await _unitOfWork.FieldDefinitions.GetBySubcategoryIdAsync(sub.Id, includeInactive: true);
            var subField = subFields.FirstOrDefault(f => f.FieldKey == categoryField.Key);
            if (subField == null) continue;

            subField.IsActive = false;
            subField.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.FieldDefinitions.UpdateAsync(subField);
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Deactivated category field definition {FieldId}", id);
        return true;
    }

    private static FieldDefinitionResponse MapToResponse(SubcategoryFieldDefinition field)
    {
        List<FieldOption>? options = null;
        if (!string.IsNullOrWhiteSpace(field.OptionsJson))
        {
            try
            {
                options = System.Text.Json.JsonSerializer.Deserialize<List<FieldOption>>(field.OptionsJson);
            }
            catch
            {
                // If deserialization fails, leave as null
            }
        }

        return new FieldDefinitionResponse
        {
            Id = field.Id,
            CategoryId = field.Subcategory?.CategoryId,
            SubcategoryId = field.SubcategoryId,
            Name = field.Name,
            Label = field.Label,
            Key = field.FieldKey,
            Type = field.Type.ToString(),
            IsRequired = field.IsRequired,
            DefaultValue = field.DefaultValue,
            Options = options,
            Min = field.Min,
            Max = field.Max,
            DisplayOrder = field.SortOrder,
            IsActive = field.IsActive,
            ScopeType = "Subcategory"
        };
    }

    private static FieldDefinitionResponse MapCategoryToResponse(CategoryFieldDefinition field)
    {
        List<FieldOption>? options = null;
        if (!string.IsNullOrWhiteSpace(field.OptionsJson))
        {
            try
            {
                options = System.Text.Json.JsonSerializer.Deserialize<List<FieldOption>>(field.OptionsJson);
            }
            catch
            {
                // If deserialization fails, leave as null
            }
        }

        return new FieldDefinitionResponse
        {
            Id = field.Id,
            CategoryId = field.CategoryId,
            SubcategoryId = null,
            Name = field.Name,
            Label = field.Label,
            Key = field.Key,
            Type = field.Type.ToString(),
            IsRequired = field.IsRequired,
            DefaultValue = field.DefaultValue,
            Options = options,
            Min = field.Min,
            Max = field.Max,
            DisplayOrder = field.SortOrder,
            IsActive = field.IsActive,
            ScopeType = "Category"
        };
    }
}
