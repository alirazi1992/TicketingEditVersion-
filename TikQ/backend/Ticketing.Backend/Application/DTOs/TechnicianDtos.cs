namespace Ticketing.Backend.Application.DTOs;

public class TechnicianResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; }
    public bool IsSupervisor { get; set; }
    /// <summary>
    /// Human-readable role label for admin UI.
    /// </summary>
    public string Role { get; set; } = "Technician";
    /// <summary>When unknown (e.g. directory-only entry), null; never 0001-01-01.</summary>
    public DateTime? CreatedAt { get; set; }
    /// <summary>
    /// Linked User.Id (for JWT sub / assignment). Null = technician cannot be assigned to tickets.
    /// </summary>
    public Guid? UserId { get; set; }
    /// <summary>
    /// List of subcategory IDs this technician has expertise in
    /// </summary>
    public List<int> SubcategoryIds { get; set; } = new List<int>();
    /// <summary>
    /// Count of coverage subcategories (for summaries).
    /// </summary>
    public int CoverageCount { get; set; }
    
    // Soft delete fields
    /// <summary>
    /// True if technician has been soft-deleted (removed from system but data retained)
    /// </summary>
    public bool IsDeleted { get; set; }
    /// <summary>
    /// When the technician was soft-deleted
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}

public class TechnicianCoverageRequest
{
    public int CategoryId { get; set; }
    public int SubcategoryId { get; set; }
}

public class TechnicianCreateRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSupervisor { get; set; } = false;
    public string? Role { get; set; }
    /// <summary>
    /// List of subcategory IDs this technician has expertise in (optional)
    /// </summary>
    public List<int>? SubcategoryIds { get; set; }
    /// <summary>
    /// Explicit coverage labels (category + subcategory pairs).
    /// </summary>
    public List<TechnicianCoverageRequest>? Coverage { get; set; }
}

public class TechnicianUpdateRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSupervisor { get; set; } = false;
    /// <summary>
    /// List of subcategory IDs this technician has expertise in (optional - replaces existing permissions)
    /// </summary>
    public List<int>? SubcategoryIds { get; set; }
}

public class UpdateTechnicianExpertiseRequest
{
    public List<int> SubcategoryIds { get; set; } = new();
}

public class TechnicianStatusUpdateRequest
{
    public bool IsActive { get; set; }
}

public class TechnicianExpertiseTagDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int SubcategoryId { get; set; }
    public string SubcategoryName { get; set; } = string.Empty;
}

public class TechnicianDirectoryItemDto
{
    public Guid TechnicianId { get; set; }
    public Guid TechnicianUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string Availability { get; set; } = "Free"; // Free | Busy
    public int InboxTotalActive { get; set; }
    public int InboxLeftActiveNonTerminal { get; set; }
    public List<TechnicianExpertiseTagDto> Expertise { get; set; } = new();
}

/// <summary>
/// Request to link a Technician record to a User account (Admin-only)
/// </summary>
public class TechnicianLinkUserRequest
{
    /// <summary>
    /// The User.Id (JWT sub) of a Technician-role user to link
    /// </summary>
    public Guid UserId { get; set; }
}