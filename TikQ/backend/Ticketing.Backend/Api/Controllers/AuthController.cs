using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ticketing.Backend.Application.Common;
using Ticketing.Backend.Application.Common.Interfaces;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Infrastructure.Auth;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string AccessCookieName = "tikq_access";

    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase) { "Admin", "Technician", "Client", "Supervisor" };
    private static readonly HashSet<string> ValidLandingPaths = new(StringComparer.OrdinalIgnoreCase) { "/admin", "/technician", "/client", "/supervisor" };

    /// <summary>Fail-safe: do not treat as authenticated if role or landingPath is missing/invalid (no fallback to Client).</summary>
    private static bool HasValidRoleAndLandingPath(string? role, string? landingPath)
    {
        return !string.IsNullOrWhiteSpace(role) && ValidRoles.Contains(role)
            && !string.IsNullOrWhiteSpace(landingPath) && ValidLandingPaths.Contains(landingPath);
    }

    private readonly IUserService _userService;
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IWindowsUserMapResolver _windowsUserMapResolver;
    private readonly IAdUserLookup _adUserLookup;
    private readonly WindowsAuthOptions _windowsAuthOptions;
    private readonly EmergencyAdminOptions _emergencyAdminOptions;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly AuthCookiesOptions _authCookiesOptions;

    public AuthController(
        IUserService userService,
        AppDbContext context,
        IWebHostEnvironment env,
        IWindowsUserMapResolver windowsUserMapResolver,
        IAdUserLookup adUserLookup,
        IOptions<WindowsAuthOptions> windowsAuthOptions,
        IOptions<EmergencyAdminOptions> emergencyAdminOptions,
        IOptions<AuthCookiesOptions> authCookiesOptions,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHasher<User> passwordHasher)
    {
        _userService = userService;
        _context = context;
        _env = env;
        _windowsUserMapResolver = windowsUserMapResolver;
        _adUserLookup = adUserLookup;
        _windowsAuthOptions = windowsAuthOptions?.Value ?? new WindowsAuthOptions();
        _emergencyAdminOptions = emergencyAdminOptions?.Value ?? new EmergencyAdminOptions();
        _authCookiesOptions = authCookiesOptions?.Value ?? new AuthCookiesOptions();
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
    }

    private SameSiteMode GetSameSiteMode()
    {
        var v = _authCookiesOptions.SameSite?.Trim();
        if (string.Equals(v, "None", StringComparison.OrdinalIgnoreCase)) return SameSiteMode.None;
        if (string.Equals(v, "Strict", StringComparison.OrdinalIgnoreCase)) return SameSiteMode.Strict;
        return SameSiteMode.Lax;
    }

    private bool GetSecure()
    {
        var v = _authCookiesOptions.SecurePolicy?.Trim();
        if (string.Equals(v, "Always", StringComparison.OrdinalIgnoreCase)) return true;
        return Request.IsHttps; // SameAsRequest (default)
    }

    private void SetAccessCookie(string token)
    {
        Response.Cookies.Append(AccessCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = GetSameSiteMode(),
            Secure = GetSecure(),
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddMinutes(30)
        });
    }

    private void ClearAccessCookie()
    {
        Response.Cookies.Append(AccessCookieName, string.Empty, new CookieOptions
        {
            Path = "/",
            SameSite = GetSameSiteMode(),
            HttpOnly = true,
            Secure = GetSecure(),
            Expires = DateTimeOffset.UtcNow.AddDays(-1)
        });
    }

    // ------------------------------
    // DEBUG: لیست یوزرها برای تست لاگین (Development only, Admin only)
    // ------------------------------
    [HttpGet("debug-users")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> GetDebugUsers([FromServices] AppDbContext context)
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        var users = await context.Users
            .Select(u => new
            {
                u.Email,
                u.FullName,
                Role = u.Role.ToString(),
                u.Department
            })
            .ToListAsync();

        return Ok(users);
    }

    // ------------------------------
    // SECURITY-CRITICAL: Register endpoint with explicit role validation
    // 
    // Role Security Rules (ENFORCED STRICTLY):
    // 1. Role MUST be explicitly provided in RegisterRequest (no defaults, no silent fallbacks)
    // 2. Role MUST be a valid UserRole enum value → HTTP 400 if invalid
    // 3. Requested role is ALWAYS persisted exactly as provided (no overrides)
    // 4. Admin role creation rules:
    //    a) ALLOWED if: This is the first user (bootstrap scenario), OR
    //    b) ALLOWED if: Caller is authenticated Admin
    //    c) FORBIDDEN (HTTP 403) otherwise
    // 5. Email conflict → HTTP 409
    // 6. Invalid role → HTTP 400 (explicit error message)
    // Body: flat JSON { "fullName", "email", "password", "role", ... } (Content-Type: application/json).
    // ------------------------------
    [HttpPost("register")]
    [Consumes("application/json")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Request body is required.", error = "VALIDATION" });
        }

        // SECURITY-CRITICAL: Validate model state
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { message = "FullName is required.", error = "VALIDATION" });
        }
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required.", error = "VALIDATION" });
        }
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new { message = "Password is required and must be at least 6 characters.", error = "VALIDATION" });
        }

        // SECURITY-CRITICAL: Role MUST be explicitly provided (cannot be null)
        if (!request.Role.HasValue)
        {
            return BadRequest(new { 
                message = "Role is required and must be explicitly specified. Valid values: Client (0), Technician (1), Admin (2)",
                error = "ROLE_REQUIRED",
                validRoles = new[] { "Client", "Technician", "Admin" }
            });
        }

        // SECURITY-CRITICAL: Explicit role validation - MUST be a valid enum value
        // This prevents invalid integer values from being processed
        var role = request.Role.Value;
        if (!Enum.IsDefined(typeof(UserRole), role))
        {
            return BadRequest(new { 
                message = "Invalid role specified. Role must be explicitly set to one of: Client (0), Technician (1), Admin (2)",
                error = "INVALID_ROLE",
                validRoles = new[] { "Client", "Technician", "Admin" },
                receivedRole = role.ToString()
            });
        }

        // SECURITY-CRITICAL: Admin role registration requires authenticated Admin user
        // ONLY authenticated Admin users can create new Admin accounts
        // Client and Technician registration remains allowed for anonymous users
        if (role == UserRole.Admin)
        {
            // Admin registration requires authentication
            if (User.Identity?.IsAuthenticated != true)
            {
                return StatusCode(403, new { 
                    message = "Admin account creation requires authentication. Only authenticated Admin users can create Admin accounts.",
                    error = "ADMIN_REGISTRATION_REQUIRES_AUTH",
                    requestedRole = role.ToString()
                });
            }

            // Verify caller is Admin
            var callerRoleClaim = User.FindFirstValue(ClaimTypes.Role);
            if (string.IsNullOrEmpty(callerRoleClaim) || 
                !Enum.TryParse<UserRole>(callerRoleClaim, out var callerRole) || 
                callerRole != UserRole.Admin)
            {
                return StatusCode(403, new { 
                    message = "Only Admin users can create Admin accounts. Your role does not have permission to create Admin users.",
                    error = "ADMIN_REGISTRATION_FORBIDDEN",
                    requestedRole = role.ToString(),
                    callerRole = callerRoleClaim ?? "unknown"
                });
            }
        }

        // Determine caller's role for authorization checks (for UserService)
        // This is used for additional validation in UserService (e.g., bootstrap check)
        UserRole callerRoleForService;
        if (User.Identity?.IsAuthenticated == true)
        {
            var roleClaim = User.FindFirstValue(ClaimTypes.Role);
            if (string.IsNullOrEmpty(roleClaim) || !Enum.TryParse<UserRole>(roleClaim, out callerRoleForService))
            {
                // Invalid role claim in token - treated as non-Admin for service-level checks
                callerRoleForService = UserRole.Client;
            }
        }
        else
        {
            // Anonymous caller - treated as non-Admin for service-level checks
            callerRoleForService = UserRole.Client;
        }

        // SECURITY-CRITICAL: Create a new request with validated non-nullable Role for UserService
        // UserService expects non-nullable Role, and we've already validated it's not null above
        var serviceRequest = new RegisterRequest
        {
            FullName = request.FullName,
            Email = request.Email,
            Password = request.Password,
            Role = role, // Use validated non-nullable role (request.Role.Value)
            PhoneNumber = request.PhoneNumber,
            Department = request.Department
        };

        // SECURITY-CRITICAL: Delegate to UserService for role authorization and persistence
        // UserService will:
        // 1. Validate email uniqueness
        // 2. Perform additional validation (defense-in-depth)
        // 3. Persist serviceRequest.Role EXACTLY as provided (no modifications)
        // NOTE: Admin authorization is already enforced above, but UserService may have additional checks
        var response = await _userService.RegisterAsync(serviceRequest, callerRoleForService);

        if (response == null)
        {
            // UserService returns null for security violations - determine the specific reason
            
            // Check email conflict first (most common case)
            var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == serviceRequest.Email.ToLower());
            if (emailExists)
            {
                return Conflict(new { 
                    message = "Email address is already registered.",
                    error = "EMAIL_EXISTS"
                });
            }

            // If we reach here and response is null, it's likely a service-level validation failure
            // Controller-level Admin authorization was already checked above, so Admin-related errors
            // should have been caught earlier. This handles other edge cases.
            // Generic error for other validation failures
            return BadRequest(new { 
                message = "Unable to register user. Please check your request and try again.",
                error = "REGISTRATION_FAILED"
            });
        }

        // SECURITY: Registration successful - role persisted exactly as requested
        // Verify the response contains the correct role
        if (response.User?.Role != role)
        {
            // SYSTEM FAILURE: Role mismatch between request and response
            return StatusCode(500, new { 
                message = "System error: Role mismatch detected. Contact administrator.",
                error = "ROLE_MISMATCH"
            });
        }

        // Set HttpOnly cookie; do not return token in body
        SetAccessCookie(response.Token);
        return Ok(new
        {
            ok = true,
            user = response.User,
            role = response.Role,
            isSupervisor = response.IsSupervisor,
            landingPath = response.LandingPath
        });
    }

    // ------------------------------
    // Login: Company DB (read-only) auth when enabled, then TikQ DB authorization; JWT with role + landingPath.
    // Body: flat JSON { "email": "...", "password": "..." } (Content-Type: application/json).
    // ------------------------------
    [HttpPost("login")]
    [Consumes("application/json")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email and password are required.", error = "VALIDATION" });
        }

        var result = await _userService.LoginAsync(request);
        switch (result.Kind)
        {
            case LoginResultKind.Forbidden:
                return StatusCode(403, new { message = "Account is disabled or inactive.", error = "USER_DISABLED" });
            case LoginResultKind.RoleNotAssigned:
                return StatusCode(403, new { message = "No TikQ role assigned for this account.", error = "ROLE_NOT_ASSIGNED" });
            case LoginResultKind.Unauthorized:
                return Unauthorized(new { message = "Invalid email or password.", error = "INVALID_CREDENTIALS" });
            case LoginResultKind.Success:
                var resp = result.Response!;
                SetAccessCookie(resp.Token);
                return Ok(new
                {
                    ok = true,
                    role = resp.Role,
                    isSupervisor = resp.IsSupervisor,
                    landingPath = resp.LandingPath,
                    user = resp.User
                });
            default:
                return Unauthorized(new { message = "Invalid email or password.", error = "INVALID_CREDENTIALS" });
        }
    }

    // ------------------------------
    // Emergency login (break-glass admin): only when EmergencyAdmin:Enabled; requires Email + Password + EmergencyKey.
    // Ensures an Admin user exists in TikQ DB and signs in. No default passwords in Production.
    // ------------------------------
    [HttpPost("emergency-login")]
    [Consumes("application/json")]
    [AllowAnonymous]
    public async Task<IActionResult> EmergencyLogin([FromBody] EmergencyLoginRequest request)
    {
        if (!_emergencyAdminOptions.Enabled)
        {
            return NotFound(new { message = "Emergency login is not enabled.", error = "NOT_AVAILABLE" });
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.EmergencyKey))
        {
            return BadRequest(new { message = "Email, Password, and EmergencyKey are required.", error = "VALIDATION" });
        }

        var opts = _emergencyAdminOptions;
        if (string.IsNullOrEmpty(opts.Key) || string.IsNullOrEmpty(opts.Password))
        {
            return StatusCode(500, new { message = "Emergency admin is not properly configured.", error = "CONFIG" });
        }

        if (!ConstantTimeEquals(request.EmergencyKey, opts.Key))
        {
            return Unauthorized(new { message = "Invalid credentials.", error = "INVALID_CREDENTIALS" });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var configEmail = opts.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(configEmail) || normalizedEmail != configEmail)
        {
            return Unauthorized(new { message = "Invalid credentials.", error = "INVALID_CREDENTIALS" });
        }

        if (!ConstantTimeEquals(request.Password, opts.Password))
        {
            return Unauthorized(new { message = "Invalid credentials.", error = "INVALID_CREDENTIALS" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                FullName = string.IsNullOrWhiteSpace(opts.FullName) ? (opts.Email ?? normalizedEmail) : opts.FullName.Trim(),
                Role = UserRole.Admin,
                PasswordHash = _passwordHasher.HashPassword(new User(), request.Password),
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        else
        {
            if (user.Role != UserRole.Admin)
            {
                user.Role = UserRole.Admin;
                await _context.SaveChangesAsync();
            }
        }

        var userDto = await _userService.GetByEmailAsync(normalizedEmail);
        if (userDto == null)
        {
            return StatusCode(500, new { message = "User resolution failed.", error = "INTERNAL" });
        }

        var token = _jwtTokenGenerator.GenerateToken(user, false, 30);
        SetAccessCookie(token);
        return Ok(new
        {
            ok = true,
            role = "Admin",
            isSupervisor = false,
            landingPath = "/admin",
            user = userDto
        });
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        var maxLen = Math.Max(aBytes.Length, bBytes.Length);
        if (aBytes.Length != bBytes.Length) return false;
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    // ------------------------------
    // Logout: clear HttpOnly cookie
    // ------------------------------
    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        ClearAccessCookie();
        return Ok(new { ok = true });
    }

    // ------------------------------
    // Diag: auth state for debugging (DEV only; 404 in Production)
    // ------------------------------
    [HttpGet("diag")]
    [AllowAnonymous]
    public IActionResult Diag()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        var hasBearer = !string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

        return Ok(new
        {
            windowsAuthEnabled = _windowsAuthOptions.IsWindowsAuthAvailable,
            windowsAuthMode = _windowsAuthOptions.EffectiveMode,
            hasAuthorizationBearer = hasBearer,
            isAuthenticated = User?.Identity?.IsAuthenticated ?? false,
            authenticationType = User?.Identity?.AuthenticationType ?? null,
            identityName = User?.Identity?.Name ?? null
        });
    }

    // ------------------------------
    // Windows login: when Windows identity is present, issue JWT cookie (same shape as login).
    // Off: middleware returns 403. Optional/Enforce: no Windows identity -> 401 with WWW-Authenticate: Negotiate.
    // ------------------------------
    [HttpGet("windows")]
    [HttpPost("windows")]
    [AllowAnonymous]
    public async Task<IActionResult> WindowsLogin()
    {
        if (!_windowsAuthOptions.IsWindowsAuthAvailable)
        {
            return StatusCode(403, new { error = "WINDOWS_AUTH_DISABLED", message = "Windows authentication is disabled. Use email/password login." });
        }
        var domainUser = User?.Identity?.Name;
        var isWindowsIdentity = User?.Identity?.AuthenticationType?.Contains("Negotiate", StringComparison.OrdinalIgnoreCase) == true;
        if (string.IsNullOrWhiteSpace(domainUser) || !isWindowsIdentity)
        {
            Response.Headers["WWW-Authenticate"] = "Negotiate";
            return Unauthorized(new { error = "WINDOWS_IDENTITY_MISSING", message = "No Windows identity. Use Windows Integrated Authentication or email/password login." });
        }
        var samAccountName = domainUser.Contains('\\')
            ? domainUser.Substring(domainUser.LastIndexOf('\\') + 1)
            : domainUser.Contains('@')
                ? domainUser.Substring(0, domainUser.IndexOf('@'))
                : domainUser;
        var email = _windowsUserMapResolver.ResolveEmail(domainUser);
        if (string.IsNullOrEmpty(email))
            email = await _adUserLookup.GetEmailBySamAccountNameAsync(samAccountName, HttpContext.RequestAborted);
        if (string.IsNullOrEmpty(email))
        {
            return StatusCode(403, new { message = "Could not resolve Windows user to an email. Contact administrator.", error = "AD_EMAIL_NOT_FOUND" });
        }
        var user = await _userService.GetByEmailAsync(email);
        if (user == null)
            return NotFound();
        var roleStr = user.Role.ToString();
        var landingPath = !string.IsNullOrWhiteSpace(user.LandingPath) ? user.LandingPath : LandingPathResolver.GetLandingPath(user.Role, user.IsSupervisor);
        if (!HasValidRoleAndLandingPath(roleStr, landingPath))
            return Unauthorized(new { error = "missing_role", message = "User has no valid role or landing path assigned." });
        var dbUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == user.Id);
        if (dbUser == null)
            return NotFound();
        var token = _jwtTokenGenerator.GenerateToken(dbUser, user.IsSupervisor, 30);
        SetAccessCookie(token);
        return Ok(new
        {
            ok = true,
            role = roleStr,
            isSupervisor = user.IsSupervisor,
            landingPath,
            user
        });
    }

    // ------------------------------
    // WhoAmI: lightweight session restore (JWT cookie tikq_access or Windows Integrated Auth)
    // ------------------------------
    [HttpGet("whoami")]
    [AllowAnonymous]
    public async Task<IActionResult> WhoAmI()
    {
        // Diagnostic header for cookie auth (safe: only indicates presence of cookie, not value)
        var cookiePresent = Request.Cookies.TryGetValue(AccessCookieName, out var cookieVal) && !string.IsNullOrEmpty(cookieVal);
        Response.Headers["X-Auth-Cookie-Present"] = cookiePresent ? "true" : "false";

        if (User?.Identity?.IsAuthenticated != true)
        {
            return Ok(new
            {
                isAuthenticated = false,
                email = (string?)null,
                role = (string?)null,
                isSupervisor = false,
                landingPath = "/login"
            });
        }

        var email = User.FindFirstValue("email") ?? User.FindFirstValue(ClaimTypes.Email);

        // JWT path: email and role/supervisor from claims (same rules as Login via LandingPathResolver)
        if (!string.IsNullOrEmpty(email))
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
            var isSupervisor = string.Equals(
                User.FindFirstValue("isSupervisor") ?? User.FindFirstValue("is_supervisor"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(role) || !Enum.TryParse<UserRole>(role, true, out var parsedRole) || !Enum.IsDefined(typeof(UserRole), parsedRole))
            {
                return Ok(new
                {
                    isAuthenticated = false,
                    authError = "missing_role",
                    email = (string?)null,
                    role = (string?)null,
                    isSupervisor = false,
                    landingPath = "/login"
                });
            }
            var landingPath = LandingPathResolver.GetLandingPath(parsedRole, isSupervisor);
            if (!HasValidRoleAndLandingPath(role, landingPath))
            {
                return Ok(new
                {
                    isAuthenticated = false,
                    authError = "missing_role",
                    email = (string?)null,
                    role = (string?)null,
                    isSupervisor = false,
                    landingPath = "/login"
                });
            }
            return Ok(new
            {
                isAuthenticated = true,
                email,
                role,
                isSupervisor,
                landingPath
            });
        }

        // Windows auth path: resolve domain user to email, then role + isSupervisor from TikQ DB
        var domainUser = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(domainUser))
        {
            return Ok(new { isAuthenticated = false, email = (string?)null, role = (string?)null, isSupervisor = false, landingPath = "/login" });
        }

        var resolvedEmail = _windowsUserMapResolver.ResolveEmail(domainUser);
        if (string.IsNullOrEmpty(resolvedEmail))
        {
            return Ok(new { isAuthenticated = false, email = (string?)null, role = (string?)null, isSupervisor = false, landingPath = "/login" });
        }

        var user = await _userService.GetByEmailAsync(resolvedEmail);
        if (user == null)
        {
            return Ok(new { isAuthenticated = false, email = (string?)null, role = (string?)null, isSupervisor = false, landingPath = "/login" });
        }

        var winRole = user.Role.ToString();
        var winLandingPath = !string.IsNullOrWhiteSpace(user.LandingPath) ? user.LandingPath : Ticketing.Backend.Application.Common.LandingPathResolver.GetLandingPath(user.Role, user.IsSupervisor);
        if (!HasValidRoleAndLandingPath(winRole, winLandingPath))
        {
            return Ok(new
            {
                isAuthenticated = false,
                authError = "missing_role",
                email = (string?)null,
                role = (string?)null,
                isSupervisor = false,
                landingPath = "/login"
            });
        }
        return Ok(new
        {
            isAuthenticated = true,
            email = user.Email,
            role = winRole,
            isSupervisor = user.IsSupervisor,
            landingPath = winLandingPath
        });
    }

    // ------------------------------
    // Me: JWT (NameIdentifier = User.Id) or Windows Integrated Auth (Identity.Name -> map -> email -> user)
    // When not authenticated, returns 401 with clear error: JWT_REQUIRED or WINDOWS_IDENTITY_MISSING.
    // ------------------------------
    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Me()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            if (!_windowsAuthOptions.IsWindowsAuthAvailable)
                return Unauthorized(new { error = "JWT_REQUIRED", message = "Windows auth is off; use email/password login to obtain JWT." });
            return Unauthorized(new { error = "WINDOWS_IDENTITY_MISSING", message = "Windows auth expected but no Windows identity was provided by IIS. Check IIS Windows Authentication settings." });
        }

        // 1) JWT path: NameIdentifier is GUID
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(idValue, out var userId))
        {
            var user = await _userService.GetByIdAsync(userId);
            if (user == null)
                return NotFound();
            var roleStr = user.Role.ToString();
            var landingPath = !string.IsNullOrWhiteSpace(user.LandingPath) ? user.LandingPath : Ticketing.Backend.Application.Common.LandingPathResolver.GetLandingPath(user.Role, user.IsSupervisor);
            if (!HasValidRoleAndLandingPath(roleStr, landingPath))
                return Unauthorized(new { error = "missing_role", message = "User has no valid role or landing path assigned." });
            return Ok(user);
        }

        // 2) Windows path: resolve DOMAIN\username (or user@domain) to email, then find TikQ user
        var domainUser = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(domainUser))
        {
            if (!_windowsAuthOptions.IsWindowsAuthAvailable)
                return Unauthorized(new { error = "JWT_REQUIRED", message = "Windows auth is off; use email/password login to obtain JWT." });
            return Unauthorized(new { error = "WINDOWS_IDENTITY_MISSING", message = "Windows auth expected but no Windows identity was provided by IIS. Check IIS Windows Authentication settings." });
        }

        var samAccountName = domainUser.Contains('\\')
            ? domainUser.Substring(domainUser.LastIndexOf('\\') + 1)
            : domainUser.Contains('@')
                ? domainUser.Substring(0, domainUser.IndexOf('@'))
                : domainUser;

        var email = _windowsUserMapResolver.ResolveEmail(domainUser);
        if (string.IsNullOrEmpty(email))
        {
            email = await _adUserLookup.GetEmailBySamAccountNameAsync(samAccountName, HttpContext.RequestAborted);
        }

        if (string.IsNullOrEmpty(email))
        {
            return StatusCode(403, new { message = "Could not resolve Windows user to an email (AD lookup and map). Contact administrator.", error = "AD_EMAIL_NOT_FOUND" });
        }

        var userByEmail = await _userService.GetByEmailAsync(email);
        if (userByEmail == null)
        {
            return NotFound();
        }

        var roleStrMe = userByEmail.Role.ToString();
        var landingPathMe = !string.IsNullOrWhiteSpace(userByEmail.LandingPath) ? userByEmail.LandingPath : Ticketing.Backend.Application.Common.LandingPathResolver.GetLandingPath(userByEmail.Role, userByEmail.IsSupervisor);
        if (!HasValidRoleAndLandingPath(roleStrMe, landingPathMe))
            return Unauthorized(new { error = "missing_role", message = "User has no valid role or landing path assigned." });
        return Ok(userByEmail);
    }

    // ------------------------------
    // Update Profile
    // ------------------------------
    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var userId))
        {
            return Unauthorized();
        }

        var user = await _userService.UpdateProfileAsync(userId, request);
        if (user == null)
        {
            return Conflict("Unable to update profile with the provided information.");
        }

        return Ok(user);
    }

    // ------------------------------
    // Change Password
    // ------------------------------
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idValue, out var userId))
        {
            return Unauthorized("کاربر احراز هویت نشده است");
        }

        var (success, errorMessage) = await _userService.ChangePasswordAsync(
            userId, 
            request.CurrentPassword, 
            request.NewPassword, 
            request.ConfirmNewPassword);

        if (!success)
        {
            return BadRequest(new { message = errorMessage ?? "رمز عبور قابل تغییر نیست" });
        }

        return Ok(new { success = true, message = "رمز عبور با موفقیت تغییر کرد" });
    }
}