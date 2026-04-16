using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/admin/categories/{categoryId}/fields")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminCategoryFieldsController : ControllerBase
{
    private readonly IFieldDefinitionService _fieldDefinitionService;
    private readonly ILogger<AdminCategoryFieldsController> _logger;

    public AdminCategoryFieldsController(
        IFieldDefinitionService fieldDefinitionService,
        ILogger<AdminCategoryFieldsController> logger)
    {
        _fieldDefinitionService = fieldDefinitionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetFields(int categoryId)
    {
        try
        {
            var fields = await _fieldDefinitionService.GetCategoryFieldDefinitionsAsync(categoryId, includeInactive: true);
            return Ok(fields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin fields for category {CategoryId}", categoryId);
            return StatusCode(500, new { message = "An error occurred while retrieving fields." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateField(int categoryId, [FromBody] CreateFieldDefinitionRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _fieldDefinitionService.CreateCategoryFieldDefinitionAsync(categoryId, request);
            if (created == null)
            {
                return NotFound(new { message = "Category not found." });
            }
            return Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{fieldId}")]
    public async Task<IActionResult> UpdateField(int categoryId, int fieldId, [FromBody] UpdateFieldDefinitionRequest request)
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
    public async Task<IActionResult> DeleteField(int categoryId, int fieldId)
    {
        var deleted = await _fieldDefinitionService.DeleteFieldDefinitionAsync(fieldId);
        if (!deleted)
        {
            return NotFound(new { message = "Field not found." });
        }
        return NoContent();
    }
}















