using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/admin/subcategories/{subcategoryId}/fields-legacy")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminSubcategoryFieldsController : ControllerBase
{
    private readonly IFieldDefinitionService _fieldDefinitionService;
    private readonly ILogger<AdminSubcategoryFieldsController> _logger;

    public AdminSubcategoryFieldsController(
        IFieldDefinitionService fieldDefinitionService,
        ILogger<AdminSubcategoryFieldsController> logger)
    {
        _fieldDefinitionService = fieldDefinitionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetFields(int subcategoryId)
    {
        try
        {
            var fields = await _fieldDefinitionService.GetFieldDefinitionsAsync(subcategoryId, includeInactive: true);
            return Ok(fields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin fields for subcategory {SubcategoryId}", subcategoryId);
            return StatusCode(500, new { message = "An error occurred while retrieving fields." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateField(int subcategoryId, [FromBody] CreateFieldDefinitionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _fieldDefinitionService.CreateFieldDefinitionAsync(subcategoryId, request);
            if (created == null)
            {
                return NotFound(new { message = "Subcategory not found." });
            }
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{fieldId}")]
    public async Task<IActionResult> UpdateField(int subcategoryId, int fieldId, [FromBody] UpdateFieldDefinitionRequest request)
    {
        try
        {
            var updated = await _fieldDefinitionService.UpdateFieldDefinitionAsync(fieldId, request);
            if (updated == null)
            {
                return NotFound(new { message = "Field not found." });
            }
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{fieldId}")]
    public async Task<IActionResult> DeleteField(int subcategoryId, int fieldId)
    {
        var deleted = await _fieldDefinitionService.DeleteFieldDefinitionAsync(fieldId);
        if (!deleted)
        {
            return NotFound(new { message = "Field not found." });
        }
        return NoContent();
    }
}















