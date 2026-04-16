using System.Text.Json.Serialization;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Domain.Entities;

public class SubcategoryFieldDefinition
{
    public int Id { get; set; }
    public int SubcategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>Stored as column FieldKey in DB (SQL Server reserved keyword). Serialized as "key" for API stability.</summary>
    [JsonPropertyName("key")]
    public string FieldKey { get; set; } = string.Empty;
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
    public Subcategory? Subcategory { get; set; }
}
