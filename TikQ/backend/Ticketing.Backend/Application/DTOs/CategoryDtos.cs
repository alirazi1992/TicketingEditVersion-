using System.ComponentModel.DataAnnotations;

namespace Ticketing.Backend.Application.DTOs;

/// <summary>Request DTO for POST /api/categories (create). Validated in controller before calling service.</summary>
public class CreateCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}

public class CategoryRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
    
    public bool IsActive { get; set; } = true;
}

public class CategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public IEnumerable<SubcategoryResponse> Subcategories { get; set; } = Enumerable.Empty<SubcategoryResponse>();
}

public class SubcategoryRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }
    
    public bool IsActive { get; set; } = true;
}

public class SubcategoryResponse
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SubcategoryDisplayCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CategoryListResponse
{
    public IEnumerable<CategoryResponse> Items { get; set; } = Enumerable.Empty<CategoryResponse>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
