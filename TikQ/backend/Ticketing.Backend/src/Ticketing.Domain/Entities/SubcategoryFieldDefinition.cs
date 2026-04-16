using System.Text.Json.Serialization;
using Ticketing.Domain.Enums;

namespace Ticketing.Domain.Entities;

public class SubcategoryFieldDefinition
{
    public int Id { get; set; }
    public int SubcategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string FieldKey { get; set; } = string.Empty;
    public FieldType Type { get; set; }
    public bool IsRequired { get; set; }
    public string? DefaultValue { get; set; }
    public string? OptionsJson { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public Subcategory? Subcategory { get; set; }
}
