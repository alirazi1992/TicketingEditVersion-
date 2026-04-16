using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Domain.Entities;

public class CategoryFieldDefinition
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public FieldType Type { get; set; }
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? OptionsJson { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Category? Category { get; set; }
}















