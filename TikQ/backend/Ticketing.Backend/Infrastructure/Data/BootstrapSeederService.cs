using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Infrastructure.Data;

/// <summary>
/// One-time bootstrap user seeding. Provider-agnostic (works with Sqlite and SqlServer).
/// Production-safe and idempotent: runs only when Bootstrap:Enabled is true and Users table is empty.
/// When enabled and Users table is empty, seeds admin (required) and optional test client/tech/supervisor.
/// No endpoints; invoked only at startup.
/// </summary>
public class BootstrapSeederService
{
    private const int MinPasswordLength = 8;

    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly BootstrapOptions _options;
    private readonly ILogger<BootstrapSeederService> _logger;

    public BootstrapSeederService(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher,
        BootstrapOptions options,
        ILogger<BootstrapSeederService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Runs bootstrap when Enabled and Users table is empty. Returns result with Seeded flag and user count.
    /// </summary>
    public async Task<BootstrapResult> RunIfEmptyAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[BOOTSTRAP] Disabled; skipping.");
            return BootstrapResult.Skipped("Bootstrap disabled.");
        }

        var userCount = await _context.Users.CountAsync(cancellationToken);
        if (userCount > 0)
        {
            _logger.LogInformation("[BOOTSTRAP] Users exist ({UserCount}); seed skipped.", userCount);
            return BootstrapResult.Skipped("Seed skipped (users already exist).");
        }

        var created = 0;
        var addedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Admin (required when Enabled and empty)
            var adminEmail = (_options.AdminEmail ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(adminEmail)) adminEmail = "admin@local";
            var adminPassword = (_options.AdminPassword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < MinPasswordLength)
            {
                _logger.LogWarning("[BOOTSTRAP] Admin password missing or too short (min {Min}); skipping bootstrap.", MinPasswordLength);
                return BootstrapResult.Skipped("Bootstrap enabled but AdminPassword missing or too short.");
            }

            await AddUserAsync(adminEmail, _options.AdminFullName?.Trim() ?? "Bootstrap Admin", UserRole.Admin, adminPassword, null, null, addedEmails, cancellationToken);
            created++;

            // Optional test client
            if (HasValidPair(_options.TestClientEmail, _options.TestClientPassword))
            {
                var email = (_options.TestClientEmail ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(email)) email = "client@local";
                if (!addedEmails.Contains(email))
                {
                    await AddUserAsync(email, "Bootstrap Client", UserRole.Client, _options.TestClientPassword!.Trim(), null, null, addedEmails, cancellationToken);
                    created++;
                }
            }

            // Optional test technician
            if (HasValidPair(_options.TestTechEmail, _options.TestTechPassword))
            {
                var email = (_options.TestTechEmail ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(email)) email = "tech@local";
                if (!addedEmails.Contains(email))
                {
                    var user = await AddUserAsync(email, "Bootstrap Technician", UserRole.Technician, _options.TestTechPassword!.Trim(), null, null, addedEmails, cancellationToken);
                    if (user != null)
                    {
                        _context.Technicians.Add(new Technician
                        {
                            Id = Guid.NewGuid(),
                            FullName = "Bootstrap Technician",
                            Email = email,
                            IsActive = true,
                            IsSupervisor = false,
                            IsDeleted = false,
                            CreatedAt = DateTime.UtcNow,
                            UserId = user.Id
                        });
                    }
                    created++;
                }
            }

            // Optional test supervisor
            if (HasValidPair(_options.TestSupervisorEmail, _options.TestSupervisorPassword))
            {
                var email = (_options.TestSupervisorEmail ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(email)) email = "supervisor@local";
                if (!addedEmails.Contains(email))
                {
                    var user = await AddUserAsync(email, "Bootstrap Supervisor", UserRole.Technician, _options.TestSupervisorPassword!.Trim(), null, null, addedEmails, cancellationToken);
                    if (user != null)
                    {
                        _context.Technicians.Add(new Technician
                        {
                            Id = Guid.NewGuid(),
                            FullName = "Bootstrap Supervisor",
                            Email = email,
                            IsActive = true,
                            IsSupervisor = true,
                            IsDeleted = false,
                            CreatedAt = DateTime.UtcNow,
                            UserId = user.Id
                        });
                    }
                    created++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("[BOOTSTRAP] Seed applied: {Count} user(s).", created);
            return BootstrapResult.SeededResult(created);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BOOTSTRAP] Bootstrap failed: {Error}", ex.Message);
            throw;
        }
    }

    private static bool HasValidPair(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return false;
        return password.Trim().Length >= MinPasswordLength;
    }

    private async Task<User?> AddUserAsync(
        string email,
        string fullName,
        UserRole role,
        string password,
        string? phone,
        string? department,
        HashSet<string> addedEmails,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var exists = await _context.Users.AnyAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail, cancellationToken);
        if (exists || addedEmails.Contains(normalizedEmail))
            return null;

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Email = normalizedEmail,
            Role = role,
            CreatedAt = DateTime.UtcNow,
            LockoutEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString(),
            PhoneNumber = phone,
            Department = department
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        _context.Users.Add(user);
        addedEmails.Add(normalizedEmail);
        _logger.LogInformation("[BOOTSTRAP] Created {Role} user: {Email}", role, normalizedEmail);
        return user;
    }
}

/// <summary>
/// Result of a bootstrap run: either skipped (with reason) or seeded with user count.
/// </summary>
public sealed class BootstrapResult
{
    public bool Seeded { get; }
    public int UsersCreated { get; }
    public string Message { get; }

    private BootstrapResult(bool seeded, int usersCreated, string message)
    {
        Seeded = seeded;
        UsersCreated = usersCreated;
        Message = message;
    }

    public static BootstrapResult Skipped(string message) => new(false, 0, message);
    public static BootstrapResult SeededResult(int usersCreated) => new(true, usersCreated, $"Seeded {usersCreated} user(s).");
}
