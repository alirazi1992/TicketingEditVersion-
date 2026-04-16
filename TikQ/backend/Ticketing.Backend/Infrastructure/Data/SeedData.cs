using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext context, IPasswordHasher<User> passwordHasher, ILogger logger)
    {
        // Deterministic seed users: upsert by email (case-insensitive) so we never duplicate
        // Required default users (roles and passwords as specified)
        await UpsertSeedUserAsync(context, passwordHasher, logger, "admin@test.com", "System Administrator", "+989000000000", "IT", UserRole.Admin, "Admin123!");
        await UpsertSeedUserAsync(context, passwordHasher, logger, "client1@test.com", "Client One", "+989000000010", "Finance", UserRole.Client, "Test123!");
        await UpsertSeedUserAsync(context, passwordHasher, logger, "tech1@test.com", "Tech One", "+989000000001", "Field Support", UserRole.Technician, "Test123!");
        await UpsertSeedUserAsync(context, passwordHasher, logger, "techsuper@email.com", "Tech Supervisor", "+989000000021", "Support", UserRole.Technician, "Test123!");
        // Additional users for ticket seed / backward compatibility
        await UpsertSeedUserAsync(context, passwordHasher, logger, "tech2@test.com", "Tech Two", "+989000000002", "Network", UserRole.Technician, "Test123!");
        await UpsertSeedUserAsync(context, passwordHasher, logger, "tech3@test.com", "Tech Three", "+989000000003", "Support", UserRole.Technician, "Test123!");
        await UpsertSeedUserAsync(context, passwordHasher, logger, "client2@test.com", "Client Two", "+989000000011", "Sales", UserRole.Client, "Test123!");
        await UpsertSeedUserAsync(context, passwordHasher, logger, "supervisor@test.com", "Supervisor One", "+989000000020", "Support", UserRole.Technician, "Test123!");
        logger.LogInformation("[SEED] seed users ensured");

        await context.SaveChangesAsync();

        // Ensure technician profiles exist for technician users (skip supervisor@test.com; handled by dedicated block below)
        const string SupervisorEmail = "supervisor@test.com";
        var technicianUsers = await context.Users
            .Where(u => u.Role == UserRole.Technician)
            .ToListAsync();

        foreach (var techUser in technicianUsers)
        {
            if (techUser.Email != null && techUser.Email.ToLower() == SupervisorEmail)
                continue;
            var existingTechnician = await context.Technicians
                .FirstOrDefaultAsync(t => t.UserId == techUser.Id || t.Email == techUser.Email);

            if (existingTechnician == null)
            {
                context.Technicians.Add(new Technician
                {
                    Id = Guid.NewGuid(),
                    FullName = techUser.FullName,
                    Email = techUser.Email ?? string.Empty,
                    Phone = techUser.PhoneNumber,
                    Department = techUser.Department,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UserId = techUser.Id
                });
            }
        }

        // Explicit supervisor flags: deterministic, do not rely on previous state
        var tech1Profile = await context.Technicians.FirstOrDefaultAsync(t => t.Email == "tech1@test.com");
        if (tech1Profile != null)
            tech1Profile.IsSupervisor = false;
        var tech2Profile = await context.Technicians.FirstOrDefaultAsync(t => t.Email == "tech2@test.com");
        if (tech2Profile != null)
            tech2Profile.IsSupervisor = false;
        var tech3Profile = await context.Technicians.FirstOrDefaultAsync(t => t.Email == "tech3@test.com");
        if (tech3Profile != null)
            tech3Profile.IsSupervisor = false;
        var techsuperProfile = await context.Technicians.FirstOrDefaultAsync(t => t.Email == "techsuper@email.com");
        if (techsuperProfile != null)
            techsuperProfile.IsSupervisor = true;

        // Dedicated supervisor: upsert Technician by Email (find by email first to avoid UNIQUE; never insert if exists)
        var supervisorUser = await context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == SupervisorEmail);
        if (supervisorUser != null)
        {
            var supervisorTech = await context.Technicians
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Email == SupervisorEmail);
            if (supervisorTech != null)
            {
                supervisorTech.UserId = supervisorUser.Id;
                supervisorTech.FullName = "Supervisor One";
                supervisorTech.Phone = "+989000000020";
                supervisorTech.Department = "Support";
                supervisorTech.IsSupervisor = true;
                supervisorTech.IsActive = true;
                supervisorTech.IsDeleted = false;
            }
            else
            {
                context.Technicians.Add(new Technician
                {
                    Id = Guid.NewGuid(),
                    FullName = "Supervisor One",
                    Email = SupervisorEmail,
                    Phone = "+989000000020",
                    Department = "Support",
                    IsActive = true,
                    IsSupervisor = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    UserId = supervisorUser.Id
                });
            }
            logger.LogInformation("[SEED] supervisor technician ensured (IsSupervisor=true)");
        }

        await context.SaveChangesAsync();

        // Supervisor–technician links for dev/demo: supervisor@test.com and techsuper@email.com see tech1, tech2 linked
        await EnsureSupervisorTechnicianLinksAsync(context, logger);

        await context.SaveChangesAsync();

        // Align backend categories with frontend slugs
        var categorySeeds = new[]
        {
            new { Id = 1, Name = "Hardware", Description = "Laptops and peripherals", Subs = new[] { "Computer Not Working", "Printer Issues", "Monitor Problems" } },
            new { Id = 2, Name = "Software", Description = "OS and application issues", Subs = new[] { "OS Issues", "Application Problems", "Software Installation" } },
            new { Id = 3, Name = "Network", Description = "Connectivity and WiFi", Subs = new[] { "Internet Connection", "WiFi Problems", "Network Drive" } },
            new { Id = 4, Name = "Email", Description = "Mailbox and clients", Subs = new[] { "Email Not Working", "Email Setup", "Email Sync" } },
            new { Id = 5, Name = "Security", Description = "Passwords and threats", Subs = new[] { "Virus / Malware", "Password Reset", "Security Incident" } },
            new { Id = 6, Name = "Access", Description = "System access and permissions", Subs = new[] { "System Access", "Permission Change", "New Account" } },
        };

        // Pass 1: Insert missing categories (with subcategories) with explicit Ids for SQL Server (non-identity).
        var categoriesToAdd = new List<Category>();
        foreach (var seed in categorySeeds)
        {
            var exists = await context.Categories.AnyAsync(c => c.Name == seed.Name);
            if (!exists)
            {
                categoriesToAdd.Add(new Category
                {
                    Id = seed.Id,
                    Name = seed.Name,
                    NormalizedName = NormalizeName(seed.Name),
                    Description = seed.Description,
                    Subcategories = seed.Subs
                        .Select((s, i) => new Subcategory
                        {
                            Id = seed.Id * 100 + (i + 1),
                            Name = s,
                            CategoryId = seed.Id
                        })
                        .ToList()
                });
            }
        }
        if (categoriesToAdd.Count > 0)
            context.Categories.AddRange(categoriesToAdd);
        await context.SaveChangesAsync();

        // Pass 2: Update NormalizedName/Description and add any missing subcategories on existing categories.
        foreach (var seed in categorySeeds)
        {
            var category = await context.Categories.Include(c => c.Subcategories)
                .FirstOrDefaultAsync(c => c.Name == seed.Name);
            if (category == null) continue;

            if (string.IsNullOrWhiteSpace(category.NormalizedName))
                category.NormalizedName = NormalizeName(category.Name);
            if (string.IsNullOrWhiteSpace(category.Description))
                category.Description = seed.Description;

            var existingSubNames = category.Subcategories.Select(sc => sc.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var nextSubIndex = category.Subcategories.Count;
            foreach (var sub in seed.Subs)
            {
                if (!existingSubNames.Contains(sub))
                {
                    nextSubIndex++;
                    category.Subcategories.Add(new Subcategory
                    {
                        Id = category.Id * 100 + nextSubIndex,
                        Name = sub,
                        CategoryId = category.Id
                    });
                }
            }
        }

        await context.SaveChangesAsync();

        // Map category ids
        var hardware = await context.Categories.Include(c => c.Subcategories).FirstAsync(c => c.Name == "Hardware");
        var software = await context.Categories.Include(c => c.Subcategories).FirstAsync(c => c.Name == "Software");
        var network = await context.Categories.Include(c => c.Subcategories).FirstAsync(c => c.Name == "Network");

        // Map users (after ensured creation)
        var tech1 = await context.Users.FirstAsync(u => u.Email == "tech1@test.com");
        var tech2 = await context.Users.FirstAsync(u => u.Email == "tech2@test.com");
        var client1 = await context.Users.FirstAsync(u => u.Email == "client1@test.com");
        var client2 = await context.Users.FirstAsync(u => u.Email == "client2@test.com");

        var technicianProfiles = await context.Technicians.ToListAsync();
        var techProfile1 = technicianProfiles.FirstOrDefault(t => t.Email == tech1.Email);
        var techProfile2 = technicianProfiles.FirstOrDefault(t => t.Email == tech2.Email);

        if (!context.Tickets.Any())
        {
            var tickets = new List<Ticket>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "VPN not connecting",
                    Description = "Cannot connect to VPN on Windows 11",
                    CategoryId = network.Id,
                    SubcategoryId = network.Subcategories.First(sc => sc.Name == "Internet Connection").Id,
                    Priority = TicketPriority.High,
                    Status = TicketStatus.Submitted,
                    CreatedByUserId = client1.Id,
                    AssignedToUserId = tech1.Id,
                    TechnicianId = techProfile1?.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Printer jam on 3rd floor",
                    Description = "Paper jam keeps returning",
                    CategoryId = hardware.Id,
                    SubcategoryId = hardware.Subcategories.First(sc => sc.Name == "Printer Issues").Id,
                    Priority = TicketPriority.Medium,
                    Status = TicketStatus.InProgress,
                    CreatedByUserId = client2.Id,
                    AssignedToUserId = tech2.Id,
                    TechnicianId = techProfile2?.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    DueDate = DateTime.UtcNow.AddDays(2)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Outlook keeps crashing",
                    Description = "Crashes when opening calendar",
                    CategoryId = software.Id,
                    SubcategoryId = software.Subcategories.First(sc => sc.Name == "Application Problems").Id,
                    Priority = TicketPriority.Critical,
                    Status = TicketStatus.InProgress,
                    CreatedByUserId = client1.Id,
                    AssignedToUserId = tech1.Id,
                    TechnicianId = techProfile1?.Id,
                    CreatedAt = DateTime.UtcNow.AddHours(-8)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Request new laptop",
                    Description = "Need new laptop for new hire",
                    CategoryId = hardware.Id,
                    SubcategoryId = hardware.Subcategories.First(sc => sc.Name == "Computer Not Working").Id,
                    Priority = TicketPriority.Low,
                    Status = TicketStatus.Open,
                    CreatedByUserId = client2.Id,
                    AssignedToUserId = tech2.Id,
                    TechnicianId = techProfile2?.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "WiFi drops in conference room",
                    Description = "Signal weak in conference area",
                    CategoryId = network.Id,
                    SubcategoryId = network.Subcategories.First(sc => sc.Name == "WiFi Problems").Id,
                    Priority = TicketPriority.High,
                    Status = TicketStatus.Solved,
                    CreatedByUserId = client2.Id,
                    AssignedToUserId = tech1.Id,
                    TechnicianId = techProfile1?.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            context.Tickets.AddRange(tickets);
            await context.SaveChangesAsync();

            var messages = new List<TicketMessage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TicketId = tickets[0].Id,
                    AuthorUserId = client1.Id,
                    Message = "Issue started after update",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    Status = TicketStatus.Submitted
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TicketId = tickets[0].Id,
                    AuthorUserId = tech1.Id,
                    Message = "Checking logs and VPN client version",
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    Status = TicketStatus.InProgress
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TicketId = tickets[2].Id,
                    AuthorUserId = tech1.Id,
                    Message = "Reinstalling Office to fix crash",
                    CreatedAt = DateTime.UtcNow.AddHours(-6),
                    Status = TicketStatus.InProgress
                }
            };

            context.TicketMessages.AddRange(messages);

        }

        // Ensure default system settings exist (idempotent)
        var existingSettings = await context.SystemSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (existingSettings == null)
        {
            var defaultSettings = new SystemSettings
            {
                Id = 1,
                AppName = "سامانه تیکتینگ",
                SupportEmail = "support@example.com",
                SupportPhone = "",
                DefaultLanguage = "fa",
                DefaultTheme = "system",
                Timezone = "Asia/Tehran",
                DefaultPriority = TicketPriority.Medium,
                DefaultStatus = TicketStatus.Submitted,
                ResponseSlaHours = 24,
                AutoAssignEnabled = false,
                AllowClientAttachments = true,
                MaxAttachmentSizeMB = 10,
                EmailNotificationsEnabled = true,
                SmsNotificationsEnabled = false,
                NotifyOnTicketCreated = true,
                NotifyOnTicketAssigned = true,
                NotifyOnTicketReplied = true,
                NotifyOnTicketClosed = true,
                PasswordMinLength = 6,
                Require2FA = false,
                SessionTimeoutMinutes = 60,
                AllowedEmailDomains = "[]",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.SystemSettings.Add(defaultSettings);
            await context.SaveChangesAsync();
        }

        logger.LogInformation("[SEED] Seed ran successfully.");
    }

    /// <summary>
    /// Find user by email (case-insensitive). If exists: update FullName, Phone, Department, Role, password; ensure SecurityStamp/Lockout defaults. If not: create and add. Idempotent.
    /// </summary>
    private static async Task UpsertSeedUserAsync(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher,
        ILogger logger,
        string email,
        string fullName,
        string? phone,
        string? department,
        UserRole role,
        string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await context.Users.FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalizedEmail);
        if (existing != null)
        {
            existing.FullName = fullName;
            existing.PhoneNumber = phone;
            existing.Department = department;
            existing.Role = role;
            existing.PasswordHash = passwordHasher.HashPassword(existing, password);
            if (string.IsNullOrEmpty(existing.SecurityStamp))
                existing.SecurityStamp = Guid.NewGuid().ToString();
            existing.LockoutEnabled = false;
            existing.LockoutEnd = null;
            logger.LogInformation("[SEED] upsert user: {Email} (updated)", existing.Email);
        }
        else
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = normalizedEmail,
                PhoneNumber = phone,
                Department = department,
                Role = role,
                CreatedAt = DateTime.UtcNow,
                PasswordHash = passwordHasher.HashPassword(new User(), password),
                SecurityStamp = Guid.NewGuid().ToString(),
                LockoutEnabled = false,
                LockoutEnd = null
            };
            context.Users.Add(user);
            logger.LogInformation("[SEED] upsert user: {Email} (created)", normalizedEmail);
        }
    }

    /// <summary>
    /// Ensures every technician in the Technicians table has a corresponding User (identity).
    /// Creates missing users (Role=Technician; supervisor claim from IsSupervisor), sets password to Test123! for all.
    /// Returns a report: Email, Roles, and "created" or "updated".
    /// </summary>
    public static async Task<IReadOnlyList<TechnicianUserSyncReport>> SyncTechnicianUsersAsync(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        const string TechnicianPassword = "Test123!";
        var report = new List<TechnicianUserSyncReport>();

        var technicians = await context.Technicians
            .Where(t => !t.IsDeleted)
            .ToListAsync();

        foreach (var tech in technicians)
        {
            if (string.IsNullOrWhiteSpace(tech.Email))
                continue;

            var user = await context.Users
                .FirstOrDefaultAsync(u => u.Id == tech.UserId || u.Email == tech.Email);

            var roles = tech.IsSupervisor ? "Technician, Supervisor" : "Technician";
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = tech.FullName,
                    Email = tech.Email,
                    Role = UserRole.Technician,
                    PhoneNumber = tech.Phone,
                    Department = tech.Department,
                    CreatedAt = DateTime.UtcNow
                };
                user.PasswordHash = passwordHasher.HashPassword(user, TechnicianPassword);
                context.Users.Add(user);
                tech.UserId = user.Id;
                report.Add(new TechnicianUserSyncReport(tech.Email, roles, "created"));
            }
            else
            {
                user.PasswordHash = passwordHasher.HashPassword(user, TechnicianPassword);
                user.Role = UserRole.Technician;
                if (tech.UserId != user.Id)
                    tech.UserId = user.Id;
                report.Add(new TechnicianUserSyncReport(tech.Email, roles, "updated"));
            }
        }

        await context.SaveChangesAsync();
        return report;
    }

    public record TechnicianUserSyncReport(string Email, string Roles, string Status);

    private static string NormalizeName(string name)
    {
        return name.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Ensures supervisor–technician links for dev/demo so supervisor list is non-empty.
    /// Links supervisor@test.com and techsuper@email.com to tech1@test.com and tech2@test.com.
    /// </summary>
    /// <summary>
    /// Runs every startup in Development. Inserts SupervisorTechnicianLinks using Users.Id (from Users table by email).
    /// Cleans dangling links (supervisor/technician UserId no longer in Users) then idempotently ensures links for current IDs.
    /// </summary>
    private static async Task EnsureSupervisorTechnicianLinksAsync(AppDbContext context, ILogger logger)
    {
        var userIds = await context.Users.Select(u => u.Id).ToListAsync();
        var dangling = await context.SupervisorTechnicianLinks
            .Where(l => !userIds.Contains(l.SupervisorUserId) || !userIds.Contains(l.TechnicianUserId))
            .ToListAsync();
        if (dangling.Count > 0)
        {
            context.SupervisorTechnicianLinks.RemoveRange(dangling);
            await context.SaveChangesAsync();
            logger.LogInformation("[SEED] SupervisorTechnicianLinks: removed {Count} dangling links (stale UserIds)", dangling.Count);
        }

        var supervisorEmails = new[] { "supervisor@test.com", "techsuper@email.com" };
        var technicianEmails = new[] { "tech1@test.com", "tech2@test.com" };
        var supSet = new HashSet<string>(supervisorEmails, StringComparer.OrdinalIgnoreCase);
        var techSet = new HashSet<string>(technicianEmails, StringComparer.OrdinalIgnoreCase);

        var supervisorUsers = await context.Users
            .Where(u => u.Email != null && supSet.Contains(u.Email))
            .ToListAsync();
        var technicianUsers = await context.Users
            .Where(u => u.Email != null && techSet.Contains(u.Email))
            .ToListAsync();

        if (supervisorUsers.Count == 0 || technicianUsers.Count == 0)
        {
            logger.LogWarning("[SEED] SupervisorTechnicianLinks skipped: supervisorUsers={SupCount}, technicianUsers={TechCount} (check emails)", supervisorUsers.Count, technicianUsers.Count);
            return;
        }

        var added = 0;
        foreach (var sup in supervisorUsers)
        {
            foreach (var tech in technicianUsers)
            {
                var exists = await context.SupervisorTechnicianLinks
                    .AnyAsync(l => l.SupervisorUserId == sup.Id && l.TechnicianUserId == tech.Id);
                if (exists) continue;

                context.SupervisorTechnicianLinks.Add(new SupervisorTechnicianLink
                {
                    Id = Guid.NewGuid(),
                    SupervisorUserId = sup.Id,
                    TechnicianUserId = tech.Id,
                    CreatedAt = DateTime.UtcNow
                });
                added++;
                logger.LogInformation("[SEED] Linked supervisor {SupEmail} (UserId={SupId}) to technician {TechEmail} (UserId={TechId})", sup.Email, sup.Id, tech.Email, tech.Id);
            }
        }

        await context.SaveChangesAsync();
        var totalLinks = await context.SupervisorTechnicianLinks.CountAsync();
        logger.LogInformation("[SEED] SupervisorTechnicianLinks: added={Added}, totalLinksInDb={TotalLinks} (supervisor@test.com + techsuper@email.com -> tech1, tech2)", added, totalLinks);
    }
}
