using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;
using Microsoft.Data.SqlClient;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private const string GenericEfSaveMessage = "An error occurred while saving the entity changes. See the inner exception for details.";

    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ICategoryService categoryService, ILogger<CategoriesController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var categories = await _categoryService.GetAllAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving categories", error = ex.Message });
        }
    }

    [HttpGet("admin")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> GetAdminCategories([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _categoryService.GetAdminCategoriesAsync(search, page, pageSize);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest? request)
    {
        _logger.LogInformation("CreateCategory: Received request - Name={Name}, Description={Description}, IsActive={IsActive}",
            request?.Name, request?.Description, request?.IsActive);

        if (request is null)
        {
            return BadRequest(new { message = "Request body is required" });
        }

        // Explicit validation: name required, trim, max length (match EF Category.Name)
        const int nameMaxLength = 200;
        var name = (request.Name ?? string.Empty).Trim();
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrEmpty(name))
        {
            errors["name"] = new[] { "Name is required" };
        }
        else if (name.Length > nameMaxLength)
        {
            errors["name"] = new[] { $"Name must be at most {nameMaxLength} characters" };
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("CreateCategory: Validation failed - Errors={Errors}", string.Join("; ", errors.SelectMany(e => e.Value)));
            return BadRequest(new { message = "Validation failed", errors });
        }

        var categoryRequest = new CategoryRequest
        {
            Name = name,
            Description = (request.Description ?? string.Empty).Trim().Length > 0 ? (request.Description ?? string.Empty).Trim() : null,
            IsActive = request.IsActive
        };

        var correlationId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var safePayload = new { Name = name, Description = categoryRequest.Description != null ? "(set)" : "(null)", categoryRequest.IsActive };

        try
        {
            var result = await _categoryService.CreateAsync(categoryRequest);
            _logger.LogInformation("CreateCategory: SUCCESS - Created category Id={Id}, Name={Name}", result?.Id, result?.Name);
            return CreatedAtAction(nameof(GetAll), new { id = result!.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("CreateCategory: InvalidOperation - Message={Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var baseEx = ex.GetBaseException();
            var (sqlEx, fullMessage) = GetSqlExceptionAndFullMessage(ex);
            _logger.LogError(ex.InnerException, "CreateCategory InnerException: {Inner}", ex.InnerException?.Message);
            _logger.LogError(ex, "CreateCategory failed. Payload: {Payload} CorrelationId={CorrelationId} Base={Base} Inner={Inner}",
                safePayload, correlationId, baseEx.Message, ex.InnerException?.Message);

            // Unique constraint (SQL Server 2627, 2601; SQLite "UNIQUE constraint failed")
            if (sqlEx != null && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
            {
                _logger.LogWarning("CreateCategory: Duplicate key - returning 409. Name={Name}", name);
                return StatusCode(409, new { message = "Category name already exists", code = "DUPLICATE_NAME" });
            }
            if (fullMessage.Contains("IX_Categories_NormalizedName", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("CreateCategory: Duplicate (by message) - returning 409. Name={Name}", name);
                return StatusCode(409, new { message = "Category name already exists", code = "DUPLICATE_NAME" });
            }

            // Invalid data: NULL, constraint, truncation -> 400
            if (fullMessage.Contains("cannot insert NULL", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("constraint", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("foreign key", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("CHECK constraint", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("String or binary data would be truncated", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("data would be truncated", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("CreateCategory: Invalid data - returning 400. BaseMessage={BaseMessage}", baseEx.Message);
                var err = SanitizeErrorForResponse(baseEx.Message, fullMessage, correlationId);
                return BadRequest(new { message = "Invalid category data", code = "INVALID_CATEGORY", error = err });
            }

            var errorText = SanitizeErrorForResponse(baseEx.Message, fullMessage, correlationId);
            return StatusCode(500, new { message = "Failed to create category", error = errorText, correlationId });
        }
        catch (Exception ex)
        {
            var baseEx = ex.GetBaseException();
            var (sqlEx, fullMessage) = GetSqlExceptionAndFullMessage(ex);
            _logger.LogError(ex.InnerException, "CreateCategory InnerException: {Inner}", ex.InnerException?.Message);
            _logger.LogError(ex, "CreateCategory failed. Payload: {Payload} CorrelationId={CorrelationId} Base={Base} Inner={Inner}",
                safePayload, correlationId, baseEx.Message, ex.InnerException?.Message);

            if (sqlEx != null && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
            {
                return StatusCode(409, new { message = "Category name already exists", code = "DUPLICATE_NAME" });
            }
            var baseMsg = baseEx.Message ?? "";
            if (fullMessage.Contains("IX_Categories_NormalizedName", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                baseMsg.Contains("IX_Categories_NormalizedName", StringComparison.OrdinalIgnoreCase) ||
                baseMsg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(409, new { message = "Category name already exists", code = "DUPLICATE_NAME" });
            }
            if (fullMessage.Contains("constraint", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("cannot insert NULL", StringComparison.OrdinalIgnoreCase) ||
                fullMessage.Contains("String or binary data would be truncated", StringComparison.OrdinalIgnoreCase) ||
                baseMsg.Contains("constraint", StringComparison.OrdinalIgnoreCase) ||
                baseMsg.Contains("cannot insert NULL", StringComparison.OrdinalIgnoreCase))
            {
                var err = SanitizeErrorForResponse(baseEx.Message, fullMessage, correlationId);
                return BadRequest(new { message = "Invalid category data", code = "INVALID_CATEGORY", error = err });
            }

            var errorText = SanitizeErrorForResponse(baseEx.Message, fullMessage, correlationId);
            return StatusCode(500, new { message = "Failed to create category", error = errorText, correlationId });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _categoryService.UpdateAsync(id, request);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var deleted = await _categoryService.DeleteAsync(id);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{categoryId}/subcategories")]
    [Authorize] // Allow all authenticated roles to read subcategories
    public async Task<IActionResult> GetSubcategories(int categoryId)
    {
        try
        {
            var subcategories = await _categoryService.GetSubcategoriesAsync(categoryId);
            return Ok(subcategories);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving subcategories", error = ex.Message });
        }
    }

    [HttpPost("{categoryId}/subcategories")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> CreateSubcategory(int categoryId, [FromBody] SubcategoryRequest request)
    {
        _logger.LogInformation("CreateSubcategory: Received request - CategoryId={CategoryId}, Name={Name}, Description={Description}, IsActive={IsActive}",
            categoryId, request?.Name, request?.Description, request?.IsActive);

        if (request is null)
        {
            return BadRequest(new { message = "Request body is required" });
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("CreateSubcategory: ModelState invalid - Errors={Errors}",
                string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _categoryService.CreateSubcategoryAsync(categoryId, request);
            if (result == null)
            {
                _logger.LogWarning("CreateSubcategory: Category not found - CategoryId={CategoryId}", categoryId);
                return NotFound(new { message = "Category not found" });
            }
            _logger.LogInformation("CreateSubcategory: SUCCESS - Created subcategory Id={Id}, Name={Name}, CategoryId={CategoryId}",
                result.Id, result.Name, result.CategoryId);
            return CreatedAtAction(nameof(GetSubcategories), new { categoryId }, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("CreateSubcategory: InvalidOperation - Message={Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateSubcategory: FAILED - Exception={ExceptionType}, Message={Message}", ex.GetType().Name, ex.Message);
            return StatusCode(500, new { message = "Failed to create subcategory", error = ex.Message });
        }
    }

    [HttpPut("subcategories/{id}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> UpdateSubcategory(int id, [FromBody] SubcategoryRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _categoryService.UpdateSubcategoryAsync(id, request);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("subcategories/{id}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> DeleteSubcategory(int id)
    {
        try
        {
            var deleted = await _categoryService.DeleteSubcategoryAsync(id);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Walk exception chain to find SqlException and build combined message (so we don't miss inner cause).</summary>
    private static (SqlException? SqlException, string FullMessage) GetSqlExceptionAndFullMessage(Exception ex)
    {
        SqlException? sqlEx = null;
        var messages = new List<string>();
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SqlException se)
                sqlEx = se;
            if (!string.IsNullOrEmpty(e.Message))
                messages.Add(e.Message);
        }
        var fullMessage = string.Join(" ", messages);
        return (sqlEx, fullMessage);
    }

    /// <summary>Never return the generic EF save message in the response; use inner chain or safe message + correlation id.</summary>
    private static string SanitizeErrorForResponse(string baseMessage, string fullMessage, string correlationId)
    {
        if (string.Equals(baseMessage, GenericEfSaveMessage, StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(fullMessage))
                return fullMessage.Trim();
            return "Database constraint error. CorrelationId: " + correlationId;
        }
        return baseMessage;
    }
}
