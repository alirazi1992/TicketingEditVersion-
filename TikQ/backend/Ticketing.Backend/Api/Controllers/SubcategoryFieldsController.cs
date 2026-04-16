using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;

namespace Ticketing.Backend.Api.Controllers;

/// <summary>
/// Client-safe endpoint for reading subcategory field definitions.
/// Returns only active fields that clients can use when creating tickets.
/// </summary>
[ApiController]
[Route("api/subcategories/{subcategoryId}/fields")]
[Authorize] // Any authenticated user (Client, Technician, Admin) can read fields
public class SubcategoryFieldsController : ControllerBase
{
    private readonly IFieldDefinitionService _fieldDefinitionService;
    private readonly ILogger<SubcategoryFieldsController> _logger;

    public SubcategoryFieldsController(
        IFieldDefinitionService fieldDefinitionService,
        ILogger<SubcategoryFieldsController> logger)
    {
        _fieldDefinitionService = fieldDefinitionService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/subcategories/{subcategoryId}/fields
    /// Returns active field definitions for a subcategory (client-safe, no admin metadata)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFields(int subcategoryId)
    {
        try
        {
            // Get only active fields (exclude inactive ones for clients)
            var fields = await _fieldDefinitionService.GetFieldDefinitionsAsync(subcategoryId, includeInactive: false);
            
            // Map to client-safe response (exclude admin-only metadata if any)
            var response = fields.Select(f => new FieldDefinitionResponse
            {
                Id = f.Id,
                CategoryId = f.CategoryId,
                SubcategoryId = f.SubcategoryId,
                Name = f.Name,
                Label = f.Label,
                Key = f.Key,
                Type = f.Type.ToString(),
                IsRequired = f.IsRequired,
                DefaultValue = f.DefaultValue,
                Options = f.Options,
                Min = f.Min,
                Max = f.Max,
                DisplayOrder = f.DisplayOrder,
                IsActive = f.IsActive,
                ScopeType = f.ScopeType
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fields for subcategory {SubcategoryId}", subcategoryId);
            return StatusCode(500, new { message = "An error occurred while retrieving fields." });
        }
    }
}







