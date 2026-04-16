using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketing.Backend.Application.Services;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/categories/{categoryId}/fields")]
[Authorize]
public class CategoryFieldsController : ControllerBase
{
    private readonly IFieldDefinitionService _fieldDefinitionService;
    private readonly ILogger<CategoryFieldsController> _logger;

    public CategoryFieldsController(
        IFieldDefinitionService fieldDefinitionService,
        ILogger<CategoryFieldsController> logger)
    {
        _fieldDefinitionService = fieldDefinitionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetFields(int categoryId)
    {
        try
        {
            var fields = await _fieldDefinitionService.GetCategoryFieldDefinitionsAsync(categoryId, includeInactive: false);
            return Ok(fields);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fields for category {CategoryId}", categoryId);
            return StatusCode(500, new { message = "An error occurred while retrieving fields." });
        }
    }
}















