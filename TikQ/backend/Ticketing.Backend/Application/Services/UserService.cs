using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Ticketing.Backend.Application.Common;
using Ticketing.Backend.Application.Common.Interfaces;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Infrastructure.Auth;
using Ticketing.Backend.Infrastructure.CompanyDirectory;

namespace Ticketing.Backend.Application.Services;

public interface IUserService
{
    // Main register method used by AuthController (with creatorRole)
    Task<AuthResponse?> RegisterAsync(RegisterRequest request, UserRole creatorRole);

    // Convenience overload (self-register: treated as Client)
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);

    /// <summary>Authenticate via Company DB (when enabled) or TikQ DB; returns 401/403 or success with token and landing path.</summary>
    Task<LoginResult> LoginAsync(LoginRequest request);
    Task<UserDto?> GetByIdAsync(Guid id);
    /// <summary>Get user by email (case-insensitive). Used e.g. for Windows Integrated Auth /me resolution.</summary>
    Task<UserDto?> GetByEmailAsync(string email);
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<IEnumerable<UserDto>> GetTechniciansAsync();
    Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, string confirmNewPassword);

    /// <summary>Admin-only: assign role for org user (TikQ DB only). Creates minimal user without password if not exists. Returns null on validation error.</summary>
    Task<AssignRoleResponse?> AssignRoleForOrgUserAsync(string email, UserRole role, bool? isSupervisor);

    /// <summary>Admin-only: get current TikQ role and isSupervisor by email. Returns null if user not in TikQ DB.</summary>
    Task<RoleMappingResponse?> GetRoleByEmailAsync(string email);

    /// <summary>Admin-only: set password for a user by email (pre-provision for server/shadow users). Returns (success, errorMessage).</summary>
    Task<(bool Success, string? ErrorMessage)> SetPasswordForUserAsync(string email, string newPassword);
}

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ITechnicianRepository _technicianRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ICompanyUserDirectory _companyDirectory;
    private readonly CompanyDirectoryOptions _companyDirectoryOptions;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        ITechnicianRepository technicianRepository,
        IUnitOfWork unitOfWork,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher<User> passwordHasher,
        ICompanyUserDirectory companyDirectory,
        CompanyDirectoryOptions companyDirectoryOptions,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _technicianRepository = technicianRepository;
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _companyDirectory = companyDirectory;
        _companyDirectoryOptions = companyDirectoryOptions;
        _logger = logger;
    }

    /// <summary>
    /// Convenience overload for self-registration (assumes non-Admin creator)
    /// SECURITY: This method requires explicit role in request - no defaults applied
    /// </summary>
    public Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        // For self-register scenarios, creator is treated as non-Admin
        // Role enforcement happens in the main RegisterAsync method
        return RegisterAsync(request, UserRole.Client);
    }

    /// <summary>
    /// SECURITY-CRITICAL: Main registration method with explicit role enforcement
    /// 
    /// Role Security Rules (ENFORCED STRICTLY - NO EXCEPTIONS):
    /// 1. request.Role is ALWAYS persisted exactly as provided (NO silent overrides, NO defaults)
    /// 2. Database User.Role field MUST match request.Role exactly (no transformations)
    /// 3. Admin role creation authorization:
    ///    a) ALLOWED if: This is the first user (bootstrap scenario), OR
    ///    b) ALLOWED if: creatorRole == UserRole.Admin
    ///    c) FORBIDDEN otherwise (returns null → HTTP 403 in controller)
    /// 4. Invalid role enum values return null (→ HTTP 400 in controller)
    /// 5. Email conflicts return null (→ HTTP 409 in controller)
    /// 
    /// CRITICAL: This method NEVER modifies request.Role - it is persisted exactly as received
    /// </summary>
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, UserRole creatorRole)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();

        // SECURITY-CRITICAL: Role MUST be explicitly provided (cannot be null)
        // This is a defense-in-depth check (controller should validate first)
        if (!request.Role.HasValue)
        {
            // Role not provided - return null to trigger HTTP 400 in controller
            return null;
        }

        // SECURITY-CRITICAL: Validate role is a valid enum value
        // This prevents invalid integer values or corrupted data from being persisted
        var role = request.Role.Value;
        if (!Enum.IsDefined(typeof(UserRole), role))
        {
            // Invalid role enum value - return null to trigger HTTP 400 in controller
            return null;
        }

        // 1) SECURITY: Check email uniqueness (required for user identification)
        var exists = await _userRepository.ExistsByEmailAsync(normalizedEmail);
        if (exists)
        {
            // Email conflict - return null to trigger HTTP 409 in controller
            return null;
        }

        // 2) SECURITY-CRITICAL: Enforce Admin role creation authorization rules
        var hasAnyUsers = await _userRepository.AnyAsync();
        var isBootstrap = !hasAnyUsers;
        var isAdminRequest = role == UserRole.Admin;
        var isCreatorAdmin = creatorRole == UserRole.Admin;

        // Admin role can ONLY be created if:
        // - Bootstrap scenario (first user in system), OR
        // - Creator is authenticated Admin
        // Any other attempt is a SECURITY VIOLATION
        if (isAdminRequest && !isBootstrap && !isCreatorAdmin)
        {
            // SECURITY VIOLATION: Admin role requested without authorization
            // Return null to trigger HTTP 403 in controller
            return null;
        }

        // 3) SECURITY-CRITICAL: Create user with role EXACTLY as provided in request
        // NO modifications, NO overrides, NO defaults, NO transformations
        // User.Role MUST equal request.Role.Value exactly
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = normalizedEmail,
            Role = role, // EXACT role from request (validated above) - CRITICAL: no modifications allowed
            PhoneNumber = request.PhoneNumber,
            Department = request.Department,
            CreatedAt = DateTime.UtcNow
        };

        // 4) Hash password securely
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        // 5) Persist user to database (Role will be stored exactly as request.Role)
        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        // 6) SECURITY: Generate JWT token with role claim from persisted user.Role (30 min for cookie)
        var userDto = await MapToDtoAsync(user);
        var roleString = user.Role.ToString();
        var landingPath = LandingPathResolver.GetLandingPath(user.Role, userDto.IsSupervisor);
        return new AuthResponse
        {
            Token = _jwtTokenGenerator.GenerateToken(user, userDto.IsSupervisor, 30),
            User = userDto,
            Role = roleString,
            IsSupervisor = userDto.IsSupervisor,
            LandingPath = landingPath
        };
    }

    /// <summary>
    /// Login: TikQ DB is system-of-record for roles and passwords. CompanyDirectory (when enabled) is read-only
    /// and provides profile only (Email, FullName, IsActive/IsDisabled). No passwords from Company DB.
    /// If user not in TikQ and found in CompanyDirectory (active), create or update shadow user in TikQ with default role (Client).
    /// Authentication is always password stored in TikQ DB (local users or pre-provisioned server users).
    /// </summary>
    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            return new LoginResult { Kind = LoginResultKind.Unauthorized };
        }

        // 1) Resolve TikQ user; if missing and CompanyDirectory enabled, try to create/update shadow from directory (no write to Company DB).
        var tikqUser = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (tikqUser == null && _companyDirectoryOptions.Enabled && !string.IsNullOrWhiteSpace(_companyDirectoryOptions.ConnectionString))
        {
            var companyUser = await _companyDirectory.GetByEmailAsync(normalizedEmail);
            if (companyUser != null && !companyUser.IsDisabled && companyUser.IsActive)
            {
                // Create or update shadow user in TikQ only. Default role Client; password must be set in TikQ (pre-provisioned by admin).
                tikqUser = await GetOrCreateShadowUserAsync(normalizedEmail, companyUser.FullName);
                _logger.LogInformation("[COMPANY_DIR] shadow user created or updated for {Email}", normalizedEmail);
            }
        }

        if (tikqUser == null)
            return new LoginResult { Kind = LoginResultKind.Unauthorized };

        if (tikqUser.LockoutEnabled && tikqUser.LockoutEnd.HasValue && tikqUser.LockoutEnd > DateTimeOffset.UtcNow)
            return new LoginResult { Kind = LoginResultKind.Unauthorized };

        // 2) Authenticate with password stored in TikQ DB only (no Company DB passwords).
        var verifyTikq = _passwordHasher.VerifyHashedPassword(tikqUser, tikqUser.PasswordHash, request.Password);
        if (verifyTikq == PasswordVerificationResult.Failed)
            return new LoginResult { Kind = LoginResultKind.Unauthorized };

        var isSup = await ResolveIsSupervisorAsync(tikqUser.Id);
        var roleStr = string.IsNullOrWhiteSpace(tikqUser.Role.ToString()) ? "Client" : tikqUser.Role.ToString();
        // Deterministic: Admin -> /admin; Technician+isSupervisor -> /supervisor; Technician -> /technician; else -> /client
        var path = LandingPathResolver.GetLandingPath(tikqUser.Role, isSup);
        if (!ValidRoleAndLandingPath(roleStr, path))
            return new LoginResult { Kind = LoginResultKind.RoleNotAssigned };

        var dto = await MapToDtoAsync(tikqUser);
        dto.LandingPath = path; // ensure user object has same path as response (single source of truth)
        var jwt = _jwtTokenGenerator.GenerateToken(tikqUser, isSup, 30);
        return new LoginResult
        {
            Kind = LoginResultKind.Success,
            Response = new AuthResponse
            {
                Token = jwt,
                User = dto,
                Role = roleStr,
                IsSupervisor = isSup,
                LandingPath = path
            }
        };
    }

    private static bool ValidRoleAndLandingPath(string role, string? landingPath)
    {
        var validRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Admin", "Technician", "Client", "Supervisor" };
        var validPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/admin", "/technician", "/client", "/supervisor" };
        return !string.IsNullOrWhiteSpace(role) && validRoles.Contains(role)
            && !string.IsNullOrWhiteSpace(landingPath) && validPaths.Contains(landingPath);
    }

    /// <summary>Create or update shadow user in TikQ from directory profile. Role defaults to Client; password is random until admin pre-provisions.</summary>
    private async Task<User> GetOrCreateShadowUserAsync(string normalizedEmail, string? fullName)
    {
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user != null)
        {
            // Update profile from directory (name only; do not overwrite role or password).
            if (!string.IsNullOrWhiteSpace(fullName) && user.FullName != fullName)
            {
                user.FullName = fullName;
                await _userRepository.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync();
            }
            return user;
        }

        user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = fullName ?? normalizedEmail,
            Role = UserRole.Client,
            PasswordHash = _passwordHasher.HashPassword(new User(), Guid.NewGuid().ToString()),
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return user;
    }

    /// <summary>Get TikQ user by email; if not found create one with Role = Client (write to TikQ DB only). Returns (user, isSupervisor). Role is always from TikQ DB; do not use any role from Company Directory.</summary>
    private async Task<(User user, bool isSupervisor)> GetOrCreateTikqUserAndSupervisorAsync(string normalizedEmail, string? fullName)
    {
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user != null)
        {
            var isSupervisor = await ResolveIsSupervisorAsync(user.Id);
            return (user, isSupervisor);
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = fullName ?? normalizedEmail,
            Role = UserRole.Client,
            PasswordHash = _passwordHasher.HashPassword(new User(), Guid.NewGuid().ToString()),
            CreatedAt = DateTime.UtcNow
        };
        await _userRepository.AddAsync(newUser);
        await _unitOfWork.SaveChangesAsync();
        return (newUser, false);
    }

    private async Task<bool> ResolveIsSupervisorAsync(Guid userId)
    {
        var technician = await _technicianRepository.GetByUserIdAsync(userId);
        return technician != null && technician.IsSupervisor;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user == null ? null : await MapToDtoAsync(user);
    }

    public async Task<UserDto?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail))
            return null;
        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        return user == null ? null : await MapToDtoAsync(user);
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _userRepository.GetAllAsync();
        var results = new List<UserDto>();
        foreach (var user in users)
        {
            results.Add(await MapToDtoAsync(user));
        }
        return results;
    }

    public async Task<IEnumerable<UserDto>> GetTechniciansAsync()
    {
        var technicians = await _userRepository.GetByRoleAsync("Technician");
        var results = new List<UserDto>();
        foreach (var user in technicians)
        {
            results.Add(await MapToDtoAsync(user));
        }
        return results;
    }

    public async Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = request.Email.ToLowerInvariant();
            var emailInUse = await _userRepository.ExistsByEmailExcludingIdAsync(normalizedEmail, userId);
            if (emailInUse)
            {
                return null;
            }

            user.Email = normalizedEmail;
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName;
        }

        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = request.PhoneNumber;
        }

        if (request.Department != null)
        {
            user.Department = request.Department;
        }

        if (request.AvatarUrl != null)
        {
            user.AvatarUrl = request.AvatarUrl;
        }

        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return await MapToDtoAsync(user);
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(
        Guid userId, 
        string currentPassword, 
        string newPassword, 
        string confirmNewPassword)
    {
        // Validate new password matches confirmation
        if (newPassword != confirmNewPassword)
        {
            return (false, "رمز عبور جدید و تکرار آن مطابقت ندارند");
        }

        // Validate password complexity
        if (newPassword.Length < 8)
        {
            return (false, "رمز عبور جدید باید حداقل ۸ کاراکتر باشد");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(newPassword, @"^(?=.*[a-zA-Z])(?=.*\d).+$"))
        {
            return (false, "رمز عبور جدید باید شامل حداقل یک حرف و یک عدد باشد");
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            return (false, "کاربر یافت نشد");
        }

        // Verify current password
        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return (false, "رمز عبور فعلی اشتباه است");
        }

        // Check if new password is different from current by verifying it
        var newPasswordVerifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, newPassword);
        if (newPasswordVerifyResult != PasswordVerificationResult.Failed)
        {
            return (false, "رمز عبور جدید باید با رمز عبور فعلی متفاوت باشد");
        }

        // Update password
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Admin-only: assign role for org user. Writes only to TikQ DB (Users/Technicians). If user does not exist, creates minimal user WITHOUT password. Ensures Technician record when role is Technician.</summary>
    public async Task<AssignRoleResponse?> AssignRoleForOrgUserAsync(string email, UserRole role, bool? isSupervisor)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail))
            return null;

        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = normalizedEmail,
                Role = role,
                PasswordHash = _passwordHasher.HashPassword(new User(), Guid.NewGuid().ToString()),
                CreatedAt = DateTime.UtcNow
            };
            await _userRepository.AddAsync(user);
        }
        else
        {
            user.Role = role;
            await _userRepository.UpdateAsync(user);
        }

        var isSup = false;
        if (role == UserRole.Technician)
        {
            var technician = await _technicianRepository.GetByUserIdAsync(user.Id);
            if (technician == null)
            {
                technician = new Technician
                {
                    Id = Guid.NewGuid(),
                    FullName = user.FullName,
                    Email = user.Email,
                    IsActive = true,
                    IsSupervisor = isSupervisor ?? false,
                    CreatedAt = DateTime.UtcNow,
                    UserId = user.Id
                };
                await _technicianRepository.AddAsync(technician);
                isSup = technician.IsSupervisor;
            }
            else
            {
                if (isSupervisor.HasValue)
                {
                    technician.IsSupervisor = isSupervisor.Value;
                    await _technicianRepository.UpdateAsync(technician);
                }
                isSup = technician.IsSupervisor;
            }
        }

        await _unitOfWork.SaveChangesAsync();
        var landingPath = LandingPathResolver.GetLandingPath(role, isSup);
        return new AssignRoleResponse
        {
            Email = normalizedEmail,
            Role = role.ToString(),
            IsSupervisor = isSup,
            LandingPath = landingPath
        };
    }

    /// <summary>Admin-only: get current TikQ role and isSupervisor by email. Returns null if user not in TikQ DB.</summary>
    public async Task<RoleMappingResponse?> GetRoleByEmailAsync(string email)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail))
            return null;

        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user == null)
            return null;

        var isSupervisor = await ResolveIsSupervisorAsync(user.Id);
        return new RoleMappingResponse
        {
            Email = normalizedEmail,
            Role = user.Role.ToString(),
            IsSupervisor = isSupervisor
        };
    }

    /// <summary>Admin-only: set password for a user by email (pre-provision for server/shadow users).</summary>
    public async Task<(bool Success, string? ErrorMessage)> SetPasswordForUserAsync(string email, string newPassword)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail))
            return (false, "Email is required.");

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 8)
            return (false, "Password must be at least 8 characters.");

        var user = await _userRepository.GetByEmailAsync(normalizedEmail);
        if (user == null)
            return (false, "User not found.");

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _userRepository.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Verify password against Company Directory stored value: PBKDF2 (ASP.NET Identity), BCrypt, or plaintext (constant-time compare). Never log stored value or password.</summary>
    private bool VerifyCompanyDirectoryPassword(string storedValue, string password)
    {
        if (string.IsNullOrEmpty(storedValue))
            return false;
        // ASP.NET Identity PBKDF2 format (base64, typically starts with "AQAAAA")
        if (storedValue.StartsWith("AQA", StringComparison.Ordinal) && storedValue.Length > 20)
        {
            var dummyUser = new User();
            return _passwordHasher.VerifyHashedPassword(dummyUser, storedValue, password) != PasswordVerificationResult.Failed;
        }
        // BCrypt: $2a$, $2b$, $2y$
        if (storedValue.Length >= 4 && storedValue[0] == '$' && storedValue[1] == '2' &&
            (storedValue[2] == 'a' || storedValue[2] == 'b' || storedValue[2] == 'y') && storedValue[3] == '$')
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, storedValue);
            }
            catch
            {
                return false;
            }
        }
        // Plaintext: constant-time compare
        var storedBytes = Encoding.UTF8.GetBytes(storedValue);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var maxLen = Math.Max(storedBytes.Length, passwordBytes.Length);
        var bufA = new byte[maxLen];
        var bufB = new byte[maxLen];
        storedBytes.AsSpan().CopyTo(bufA);
        passwordBytes.AsSpan().CopyTo(bufB);
        return CryptographicOperations.FixedTimeEquals(bufA, bufB) && storedBytes.Length == passwordBytes.Length;
    }

    private async Task<UserDto> MapToDtoAsync(User user)
    {
        var isSupervisor = false;
        if (user.Role == UserRole.Technician)
        {
            var technician = await _technicianRepository.GetByUserIdAsync(user.Id);
            isSupervisor = technician != null && technician.IsSupervisor;
        }

        var landingPath = LandingPathResolver.GetLandingPath(user.Role, isSupervisor);
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            PhoneNumber = user.PhoneNumber,
            Department = user.Department,
            AvatarUrl = user.AvatarUrl,
            IsSupervisor = isSupervisor,
            LandingPath = landingPath
        };
    }
}
