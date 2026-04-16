using System.ComponentModel.DataAnnotations;
using Ticketing.Domain.Enums;

namespace Ticketing.Application.DTOs;

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
// CRITICAL: Using class instead of record to allow proper validation attributes.
// With records, missing "Role" in JSON defaults to 0 (Client), which is a security issue.
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

public record LoginRequest(string Email, string Password);

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
}
