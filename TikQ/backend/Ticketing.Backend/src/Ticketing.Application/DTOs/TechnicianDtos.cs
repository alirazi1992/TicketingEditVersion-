namespace Ticketing.Application.DTOs;

public class TechnicianResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>
    /// Linked User.Id (for JWT sub / assignment). Null = technician cannot be assigned to tickets.
    /// </summary>
    public Guid? UserId { get; set; }
    /// <summary>
    /// List of subcategory IDs this technician has expertise in
    /// </summary>
    public List<int> SubcategoryIds { get; set; } = new List<int>();
}

public class TechnicianCreateRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// List of subcategory IDs this technician has expertise in (optional)
    /// </summary>
    public List<int>? SubcategoryIds { get; set; }
}

public class TechnicianUpdateRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// List of subcategory IDs this technician has expertise in (optional - replaces existing permissions)
    /// </summary>
    public List<int>? SubcategoryIds { get; set; }
}

public class TechnicianStatusUpdateRequest
{
    public bool IsActive { get; set; }
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