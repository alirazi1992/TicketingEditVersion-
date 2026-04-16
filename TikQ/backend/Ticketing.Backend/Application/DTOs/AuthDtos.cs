using System.ComponentModel.DataAnnotations;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.DTOs;

// SECURITY-CRITICAL: RegisterRequest with explicit role requirement
// 
// Role Handling Rules:
// - Role field is REQUIRED (no default value, must be explicitly provided)
// - Role MUST be a valid UserRole enum value (Client, Technician, or Admin)
// - Invalid role values will result in HTTP 400 Bad Request
// - The provided role is persisted exactly as specified (no modifications)
// - Role MUST be explicitly set in request body - if missing, validation will fail
// 
// NOTE: Admin role creation requires special authorization (see AuthController/UserService)
//
// Supported JSON shapes: { "fullName", "email", "password", "role", ... } or { "request": { ... } }.
// CRITICAL: Using class instead of record to allow proper validation attributes.
public class RegisterRequest
{
    [Required(ErrorMessage = "FullName is required")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    // SECURITY-CRITICAL: Role is REQUIRED - must be explicitly provided in JSON
    // Using nullable to detect when Role is not provided in request body
    // Controller will validate it's not null and is a valid enum value
    [Required(ErrorMessage = "Role is required and must be explicitly specified. Valid values: Client (0), Technician (1), Admin (2)")]
    public UserRole? Role { get; set; }

    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
}

/// <summary>Optional wrapper for register: { "request": { "fullName", "email", "password", "role", ... } }.</summary>
public class RegisterRequestWrapper
{
    public RegisterRequest? Request { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public UserRole? Role { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
}

/// <summary>Login body: { "email": "...", "password": "..." } or { "request": { "email": "...", "password": "..." } }.</summary>
public class LoginRequest
{
    [Required(ErrorMessage = "Email is required")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Optional wrapper to support { "request": { "email": "...", "password": "..." } }.</summary>
public class LoginRequestWrapper
{
    public LoginRequest? Request { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}

/// <summary>
/// Result of login for controller to map to 200/401/403. Do not leak email existence or reason.
/// </summary>
public enum LoginResultKind
{
    Success,
    Unauthorized, // Invalid credentials or user not in directory when Enforce
    Forbidden,    // User disabled or inactive in company directory
    RoleNotAssigned // CompanyDirectory enabled but TikQ user missing or has no role
}

public class LoginResult
{
    public LoginResultKind Kind { get; init; }
    public AuthResponse? Response { get; init; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "رمز عبور فعلی الزامی است")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "رمز عبور جدید الزامی است")]
    [MinLength(8, ErrorMessage = "رمز عبور جدید باید حداقل ۸ کاراکتر باشد")]
    [RegularExpression(@"^(?=.*[a-zA-Z])(?=.*\d).+$", ErrorMessage = "رمز عبور جدید باید شامل حداقل یک حرف و یک عدد باشد")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "تکرار رمز عبور الزامی است")]
    [Compare(nameof(NewPassword), ErrorMessage = "رمز عبور جدید و تکرار آن مطابقت ندارند")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? AvatarUrl { get; set; }
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto? User { get; set; }
    /// <summary>Role string for client (e.g. "Admin", "Technician", "Client").</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>True if user is a supervisor (Technicians.IsSupervisor).</summary>
    public bool IsSupervisor { get; set; }
    /// <summary>Landing path: /admin, /supervisor, /technician, or /client.</summary>
    public string LandingPath { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Department { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsSupervisor { get; set; }
    /// <summary>Landing path for routing: /admin, /supervisor, /technician, or /client.</summary>
    public string LandingPath { get; set; } = string.Empty;
}

// --- Admin role assignment (Company Directory handoff) ---

/// <summary>Request for POST /api/admin/roles/assign. Writes only to TikQ DB (Users/Technicians).</summary>
public class AssignRoleRequest
{
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool? IsSupervisor { get; set; }
}

/// <summary>Result of role assignment (role + landing path).</summary>
public class AssignRoleResponse
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsSupervisor { get; set; }
    public string LandingPath { get; set; } = string.Empty;
}

/// <summary>Response for GET /api/admin/roles/by-email. Current TikQ role and isSupervisor.</summary>
public class RoleMappingResponse
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsSupervisor { get; set; }
}

/// <summary>Request for POST /api/admin/roles/set-password. Admin pre-provision password for server/shadow users.</summary>
public class SetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Request for POST /api/auth/emergency-login. Break-glass admin: Email + Password + EmergencyKey.</summary>
public class EmergencyLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string EmergencyKey { get; set; } = string.Empty;
}
