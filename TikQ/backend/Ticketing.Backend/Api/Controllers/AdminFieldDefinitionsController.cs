using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Api.Controllers;

/// <summary>
/// Admin-only endpoints for managing subcategory field definitions.
/// Routes match frontend expectations: /api/admin/subcategories/{id}/fields
/// </summary>
[ApiController]
[Route("api/admin/subcategories/{subcategoryId}/fields")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminFieldDefinitionsController : ControllerBase
{
    private readonly IFieldDefinitionService _fieldDefinitionService;
    private readonly ILogger<AdminFieldDefinitionsController> _logger;

    public AdminFieldDefinitionsController(
        IFieldDefinitionService fieldDefinitionService,
        ILogger<AdminFieldDefinitionsController> logger)
    {
        _fieldDefinitionService = fieldDefinitionService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/subcategories/{subcategoryId}/fields
    /// Returns all fields for a subcategory
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFields(int subcategoryId)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        
        _logger.LogInformation(
            "[AdminFieldDefinitions] GetFields called - SubcategoryId: {SubcategoryId}, User: {UserEmail} ({UserId})",
            subcategoryId, userEmail, userId);

        try
        {
            var fields = await _fieldDefinitionService.GetFieldDefinitionsAsync(subcategoryId, includeInactive: true);
            var response = fields.ToList();
            
            _logger.LogInformation(
                "[AdminFieldDefinitions] GetFields success - SubcategoryId: {SubcategoryId}, Count: {Count}",
                subcategoryId, response.Count);
            
            return Ok(response);
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "[AdminFieldDefinitions] GetFields database error - SubcategoryId: {SubcategoryId}, Error: {Error}, Inner: {InnerError}",
                subcategoryId, dbEx.Message, dbEx.InnerException?.Message);
            
            // Check if it's a schema error (missing column)
            var errorMessage = dbEx.InnerException?.Message ?? dbEx.Message;
            if (errorMessage.Contains("no such column") || errorMessage.Contains("DefaultValue") || 
                errorMessage.Contains("schema") || errorMessage.Contains("column"))
            {
                // Extract column name if possible
                var columnMatch = System.Text.RegularExpressions.Regex.Match(
                    errorMessage, @"no such column:?\s*(\w+)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var columnName = columnMatch.Success ? columnMatch.Groups[1].Value : "unknown";
                
                // In Development, provide detailed error; in Production, provide safe message
                var isDevelopment = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>().IsDevelopment();
                
                if (isDevelopment)
                {
                    return StatusCode(500, new ProblemDetails
                    {
                        Status = 500,
                        Title = "Database Schema Error",
                        Detail = $"Missing column '{columnName}' in SubcategoryFieldDefinitions table. The schema guard will attempt to fix this on next startup.",
                        Instance = HttpContext.Request.Path,
                        Extensions = 
                        {
                            { "error", errorMessage },
                            { "missingColumn", columnName },
                            { "fix", "Restart the backend server - the schema guard will automatically add missing columns." }
                        }
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        message = "Database schema needs upgrade. Please contact the administrator.",
                        error = "SCHEMA_UPGRADE_REQUIRED"
                    });
                }
            }
            
            return StatusCode(500, new
            {
                message = "Database error occurred while retrieving fields. Please ensure migrations are applied.",
                error = dbEx.Message,
                innerError = dbEx.InnerException?.Message
            });
        }
        catch (Microsoft.Data.Sqlite.SqliteException sqliteEx)
        {
            _logger.LogError(sqliteEx,
                "[AdminFieldDefinitions] GetFields SQLite error - SubcategoryId: {SubcategoryId}, Error: {Error}",
                subcategoryId, sqliteEx.Message);
            
            // Check for schema errors
            if (sqliteEx.Message.Contains("no such column") || sqliteEx.Message.Contains("DefaultValue") ||
                sqliteEx.Message.Contains("schema"))
            {
                var columnMatch = System.Text.RegularExpressions.Regex.Match(
                    sqliteEx.Message, @"no such column:?\s*(\w+)", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                var columnName = columnMatch.Success ? columnMatch.Groups[1].Value : "unknown";
                
                _logger.LogWarning(
                    "[AdminFieldDefinitions] Schema error detected - {ColumnName} column missing. Schema guard should fix on next startup.",
                    columnName);
                
                // In Development, provide detailed error; in Production, provide safe message
                var isDevelopment = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>().IsDevelopment();
                
                if (isDevelopment)
                {
                    return StatusCode(500, new ProblemDetails
                    {
                        Status = 500,
                        Title = "Database Schema Error",
                        Detail = $"Missing column '{columnName}' in SubcategoryFieldDefinitions table. The schema guard will attempt to fix this on next startup.",
                        Instance = HttpContext.Request.Path,
                        Extensions = 
                        {
                            { "error", sqliteEx.Message },
                            { "missingColumn", columnName },
                            { "fix", "Restart the backend server - the schema guard will automatically add missing columns." }
                        }
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        message = "Database schema needs upgrade. Please contact the administrator.",
                        error = "SCHEMA_UPGRADE_REQUIRED"
                    });
                }
            }
            
            return StatusCode(500, new
            {
                message = "Database error occurred while retrieving fields.",
                error = sqliteEx.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[AdminFieldDefinitions] GetFields unexpected error - SubcategoryId: {SubcategoryId}, Type: {Type}, Error: {Error}",
                subcategoryId, ex.GetType().Name, ex.Message);
            
            return StatusCode(500, new
            {
                message = "An error occurred while retrieving fields",
                error = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// GET /api/admin/subcategories/{subcategoryId}/fields/{fieldId}
    /// Returns a specific field by ID
    /// </summary>
    [HttpGet("{fieldId}")]
    public async Task<IActionResult> GetField(int subcategoryId, int fieldId)
    {
        _logger.LogInformation(
            "[AdminFieldDefinitions] GetField called - SubcategoryId: {SubcategoryId}, FieldId: {FieldId}",
            subcategoryId, fieldId);

        try
        {
            var field = await _fieldDefinitionService.GetFieldDefinitionAsync(fieldId);
            
            if (field == null)
            {
                _logger.LogWarning(
                    "[AdminFieldDefinitions] GetField not found - SubcategoryId: {SubcategoryId}, FieldId: {FieldId}",
                    subcategoryId, fieldId);
                return NotFound(new { message = "Field not found", error = "FIELD_NOT_FOUND" });
            }

            if (field.SubcategoryId != subcategoryId)
            {
                _logger.LogWarning(
                    "[AdminFieldDefinitions] GetField subcategory mismatch - SubcategoryId: {SubcategoryId}, FieldSubcategoryId: {FieldSubcategoryId}",
                    subcategoryId, field.SubcategoryId);
                return BadRequest(new { message = "Field does not belong to this subcategory" });
            }

            return Ok(field);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[AdminFieldDefinitions] GetField error - SubcategoryId: {SubcategoryId}, FieldId: {FieldId}",
                subcategoryId, fieldId);
            return StatusCode(500, new { message = "An error occurred while retrieving the field", error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/admin/subcategories/{subcategoryId}/fields
    /// Creates a new field definition
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateField(int subcategoryId, [FromBody] CreateFieldDefinitionRequest? request)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? "unknown";
        
        _logger.LogInformation(
            "[AdminFieldDefinitions] CreateField called - SubcategoryId: {SubcategoryId}, Key: {Key}, Label: {Label}, Type: {Type}, User: {UserEmail}",
            subcategoryId, request?.Key, request?.Label, request?.Type, userEmail);

        if (request == null)
        {
            _logger.LogWarning("[AdminFieldDefinitions] CreateField - Request body is null for SubcategoryId: {SubcategoryId}", subcategoryId);
            return BadRequest(new { message = "Request body is required" });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _fieldDefinitionService.CreateFieldDefinitionAsync(subcategoryId, request);
            
            if (result == null)
            {
                _logger.LogWarning(
                    "[AdminFieldDefinitions] CreateField - Subcategory not found: {SubcategoryId}",
                    subcategoryId);
                return NotFound(new { message = "Subcategory not found", error = "SUBCATEGORY_NOT_FOUND" });
            }

            _logger.LogInformation(
                "[AdminFieldDefinitions] CreateField success - SubcategoryId: {SubcategoryId}, FieldId: {FieldId}",
                subcategoryId, result.Id);

            return CreatedAtAction(nameof(GetField), new { subcategoryId, fieldId = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "[AdminFieldDefinitions] CreateField validation error - SubcategoryId: {SubcategoryId}, Error: {Error}",
                subcategoryId, ex.Message);
            
            var errorCode = "VALIDATION_ERROR";
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                errorCode = "FIELD_DUPLICATE";
            }
            else if (ex.Message.Contains("requires", StringComparison.OrdinalIgnoreCase) && 
                     ex.Message.Contains("option", StringComparison.OrdinalIgnoreCase))
            {
                errorCode = "SELECT_FIELD_NO_OPTIONS";
            }

            return BadRequest(new
            {
                message = ex.Message,
                error = errorCode
            });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "[AdminFieldDefinitions] CreateField database error - SubcategoryId: {SubcategoryId}",
                subcategoryId);
            
            return StatusCode(500, new
            {
                message = "Database error occurred while creating the field. Please ensure migrations are applied.",
                error = dbEx.Message,
                innerError = dbEx.InnerException?.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[AdminFieldDefinitions] CreateField unexpected error - SubcategoryId: {SubcategoryId}",
                subcategoryId);
            
            return StatusCode(500, new
            {
                message = "An error occurred while creating the field",
                error = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// PUT /api/admin/subcategories/{subcategoryId}/fields/{fieldId}
    /// Updates an existing field definition
    /// </summary>
    [HttpPut("{fieldId}")]
    public async Task<IActionResult> UpdateField(int subcategoryId, int fieldId, [FromBody] UpdateFieldDefinitionRequest? request)
    {
        _logger.LogInformation(
            "[AdminFieldDefinitions] UpdateField called - SubcategoryId: {SubcategoryId}, FieldId: {FieldId}",
            subcategoryId, fieldId);

        if (request == null)
        {
            return BadRequest(new { message = "Request body is required" });
        }

        try
        {
            // Get existing field to verify it belongs to this subcategory
            var existingField = await _fieldDefinitionService.GetFieldDefinitionAsync(fieldId);
            
            if (existingField == null)
            {
                return NotFound(new { message = "Field not found", error = "FIELD_NOT_FOUND" });
            }

            if (existingField.SubcategoryId != subcategoryId)
            {
                return BadRequest(new { message = "Field does not belong to this subcategory" });
            }

            var result = await _fieldDefinitionService.UpdateFieldDefinitionAsync(fieldId, request);
            
            if (result == null)
            {
                return NotFound(new { message = "Field not found", error = "FIELD_NOT_FOUND" });
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "[AdminFieldDefinitions] UpdateField validation error - FieldId: {FieldId}, Error: {Error}",
                fieldId, ex.Message);
            
            var errorCode = "VALIDATION_ERROR";
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                errorCode = "FIELD_DUPLICATE";
            }

            return BadRequest(new
            {
                message = ex.Message,
                error = errorCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[AdminFieldDefinitions] UpdateField error - FieldId: {FieldId}",
                fieldId);
            return StatusCode(500, new { message = "An error occurred while updating the field", error = ex.Message });
        }
    }

    /// <summary>
    /// DELETE /api/admin/subcategories/{subcategoryId}/fields/{fieldId}
    /// Deletes a field definition
    /// </summary>
    [HttpDelete("{fieldId}")]
    public async Task<IActionResult> DeleteField(int subcategoryId, int fieldId)
    {
        _logger.LogInformation(
            "[AdminFieldDefinitions] DeleteField called - SubcategoryId: {SubcategoryId}, FieldId: {FieldId}",
            subcategoryId, fieldId);

        try
        {
            // Verify field belongs to subcategory
            var field = await _fieldDefinitionService.GetFieldDefinitionAsync(fieldId);
            
            if (field == null)
            {
                return NotFound(new { message = "Field not found", error = "FIELD_NOT_FOUND" });
            }

            if (field.SubcategoryId != subcategoryId)
            {
                return BadRequest(new { message = "Field does not belong to this subcategory" });
            }

            var deleted = await _fieldDefinitionService.DeleteFieldDefinitionAsync(fieldId);
            
            if (!deleted)
            {
                return NotFound(new { message = "Field not found", error = "FIELD_NOT_FOUND" });
            }

            _logger.LogInformation(
                "[AdminFieldDefinitions] DeleteField success - FieldId: {FieldId}",
                fieldId);

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "[AdminFieldDefinitions] DeleteField validation error - FieldId: {FieldId}, Error: {Error}",
                fieldId, ex.Message);
            
            return BadRequest(new { message = ex.Message, error = "VALIDATION_ERROR" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[AdminFieldDefinitions] DeleteField error - FieldId: {FieldId}",
                fieldId);
            return StatusCode(500, new { message = "An error occurred while deleting the field", error = ex.Message });
        }
    }
}
