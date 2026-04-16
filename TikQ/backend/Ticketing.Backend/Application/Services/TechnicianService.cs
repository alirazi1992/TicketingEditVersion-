using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Services;

/// <summary>
/// Result of linking a Technician to a User account
/// </summary>
public enum LinkUserResult
{
    Success,
    TechnicianNotFound,
    UserNotFound,
    UserNotTechnicianRole,
    AlreadyLinked
}

/// <summary>
/// Result of soft deleting a technician
/// </summary>
public enum SoftDeleteResult
{
    Success,
    TechnicianNotFound,
    AlreadyDeleted,
    Failed
}

public interface ITechnicianService
{
    Task<IEnumerable<TechnicianResponse>> GetAllTechniciansAsync(bool includeDeleted = false);
    Task<TechnicianResponse?> GetTechnicianByIdAsync(Guid id);
    Task<TechnicianResponse?> GetTechnicianByUserIdAsync(Guid userId); // Get technician by linked User.Id
    Task<IEnumerable<TechnicianResponse>> GetAssignableTechniciansAsync(); // Get active, non-supervisor technicians for delegation
    Task<IEnumerable<TechnicianDirectoryItemDto>> GetTechnicianDirectoryAsync(string? search, string? availability, int? categoryId, int? subcategoryId);
    /// <summary>Same source as Admin directory (no category/subcategory/availability filter). Includes Users with Role=Technician even without Technician row. Excludes excludeUserId.</summary>
    Task<IEnumerable<TechnicianDirectoryItemDto>> GetActiveTechnicianDirectoryForSupervisorAsync(Guid? excludeUserId);
    Task<TechnicianResponse> CreateTechnicianAsync(TechnicianCreateRequest request);
    Task<TechnicianResponse?> UpdateTechnicianAsync(Guid id, TechnicianUpdateRequest request);
    Task<TechnicianResponse?> UpdateTechnicianExpertiseAsync(Guid id, List<int> subcategoryIds);
    Task<bool> UpdateTechnicianStatusAsync(Guid id, bool isActive);
    Task<bool> IsTechnicianActiveAsync(Guid id);
    Task<(LinkUserResult result, TechnicianResponse? technician)> LinkUserAsync(Guid technicianId, Guid userId);
    
    /// <summary>
    /// Soft deletes a technician: sets IsDeleted=true, locks out the linked user account
    /// </summary>
    Task<(SoftDeleteResult result, TechnicianResponse? technician)> SoftDeleteTechnicianAsync(Guid technicianId, Guid adminUserId);
}

public class TechnicianService : ITechnicianService
{
    private readonly ITechnicianRepository _technicianRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ILogger<TechnicianService> _logger;
    private readonly IHostEnvironment _env;

    public TechnicianService(
        ITechnicianRepository technicianRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ICategoryRepository categoryRepository,
        IPasswordHasher<User> passwordHasher,
        ILogger<TechnicianService> logger,
        IHostEnvironment env)
    {
        _technicianRepository = technicianRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _categoryRepository = categoryRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
        _env = env;
    }

    public async Task<IEnumerable<TechnicianResponse>> GetAllTechniciansAsync(bool includeDeleted = false)
    {
        try
        {
            // Note: Global query filter excludes IsDeleted=true by default
            // For includeDeleted=true, we need to use IgnoreQueryFilters in repository
            var technicians = includeDeleted 
                ? await _technicianRepository.GetAllIncludingDeletedAsync()
                : await _technicianRepository.GetAllAsync();
            
            var responses = new List<TechnicianResponse>();
            foreach (var tech in technicians)
            {
                try
                {
                    // Try to load with includes, but don't fail if it doesn't work
                    Technician? techWithIncludes = null;
                    try
                    {
                        techWithIncludes = await _technicianRepository.GetByIdWithIncludesAsync(tech.Id);
                    }
                    catch (Exception includeEx)
                    {
                        _logger.LogWarning(includeEx, "Failed to load includes for technician {TechnicianId}, using basic data", tech.Id);
                    }
                    
                    responses.Add(MapToResponse(techWithIncludes ?? tech));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process technician {TechnicianId}: {Message}", tech.Id, ex.Message);
                    // Skip this technician if processing fails completely
                }
            }
            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all technicians: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<TechnicianResponse?> GetTechnicianByIdAsync(Guid id)
    {
        var technician = await _technicianRepository.GetByIdWithIncludesAsync(id);
        return technician == null ? null : MapToResponse(technician);
    }

    public async Task<IEnumerable<TechnicianDirectoryItemDto>> GetTechnicianDirectoryAsync(
        string? search,
        string? availability,
        int? categoryId,
        int? subcategoryId)
    {
        var technicians = await _technicianRepository.GetActiveWithUserIdAsync();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            technicians = technicians.Where(t =>
                t.FullName.ToLowerInvariant().Contains(normalized) ||
                t.Email.ToLowerInvariant().Contains(normalized) ||
                (t.Department ?? "").ToLowerInvariant().Contains(normalized));
        }

        List<int>? categorySubcategoryIds = null;
        if (categoryId.HasValue)
        {
            var categories = await _categoryRepository.GetAllAsync();
            var category = categories.FirstOrDefault(c => c.Id == categoryId.Value);
            if (category == null)
            {
                return new List<TechnicianDirectoryItemDto>();
            }
            categorySubcategoryIds = category.Subcategories.Select(s => s.Id).ToList();
        }

        var results = new List<TechnicianDirectoryItemDto>();
        foreach (var technician in technicians)
        {
            if (!technician.UserId.HasValue)
            {
                continue;
            }

            var permissions = await _unitOfWork.TechnicianSubcategoryPermissions.GetByTechnicianIdAsync(technician.Id);

            if (subcategoryId.HasValue && !permissions.Any(p => p.SubcategoryId == subcategoryId.Value))
            {
                continue;
            }

            if (categorySubcategoryIds != null &&
                !permissions.Any(p => categorySubcategoryIds.Contains(p.SubcategoryId)))
            {
                continue;
            }

            var assignments = await _unitOfWork.TicketTechnicianAssignments.GetActiveTicketsForTechnicianAsync(technician.UserId.Value);
            var total = assignments.Count();
            var left = assignments.Count(a => a.Ticket != null && !IsTerminalStatus(a.Ticket!.Status));
            var availabilityLabel = left <= 3 ? "Free" : "Busy";

            if (!string.IsNullOrWhiteSpace(availability) &&
                !availability.Equals("all", StringComparison.OrdinalIgnoreCase) &&
                !availability.Equals(availabilityLabel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var expertise = permissions
                .Where(p => p.Subcategory != null && p.Subcategory.Category != null)
                .Select(p => new TechnicianExpertiseTagDto
                {
                    CategoryId = p.Subcategory!.Category!.Id,
                    CategoryName = p.Subcategory!.Category!.Name,
                    SubcategoryId = p.Subcategory.Id,
                    SubcategoryName = p.Subcategory.Name
                })
                .DistinctBy(p => new { p.CategoryId, p.SubcategoryId })
                .ToList();

            results.Add(new TechnicianDirectoryItemDto
            {
                TechnicianId = technician.Id,
                TechnicianUserId = technician.UserId.Value,
                Name = technician.FullName,
                Email = technician.Email,
                Department = technician.Department,
                Availability = availabilityLabel,
                InboxTotalActive = total,
                InboxLeftActiveNonTerminal = left,
                Expertise = expertise
            });
        }

        // Admin fallback: when permission-based filtering returns zero, return all active technicians so admin can always assign.
        // Use availability: null so dev handoff (unpopulated availability) does not hide everyone.
        if ((categoryId.HasValue || subcategoryId.HasValue) && results.Count == 0)
        {
            if (_env.IsDevelopment())
            {
                _logger.LogInformation(
                    "[TECHNICIAN_DIRECTORY] No technicians matched category/subcategory (CategoryId={CategoryId}, SubcategoryId={SubcategoryId}); returning all active technicians as fallback (availability filter dropped).",
                    categoryId, subcategoryId);
            }
            return await GetTechnicianDirectoryAsync(search, null, null, null);
        }

        return results.OrderBy(r => r.Name).ToList();
    }

    /// <summary>Active technician directory for supervisor: same as Admin directory (no permissions filter), plus Users with Role=Technician not in Technicians table. Excludes excludeUserId.</summary>
    public async Task<IEnumerable<TechnicianDirectoryItemDto>> GetActiveTechnicianDirectoryForSupervisorAsync(Guid? excludeUserId)
    {
        var directory = (await GetTechnicianDirectoryAsync(null, null, null, null)).ToList();
        var userIdsInDirectory = directory.Select(d => d.TechnicianUserId).ToHashSet();
        var technicianUsers = (await _userRepository.GetByRoleAsync(UserRole.Technician)).ToList();
        var extra = technicianUsers
            .Where(u => u.Id != excludeUserId && !userIdsInDirectory.Contains(u.Id))
            .Select(u => new TechnicianDirectoryItemDto
            {
                TechnicianId = default,
                TechnicianUserId = u.Id,
                Name = u.FullName,
                Email = u.Email ?? string.Empty,
                Department = u.Department,
                Availability = "Free",
                InboxTotalActive = 0,
                InboxLeftActiveNonTerminal = 0,
                Expertise = new List<TechnicianExpertiseTagDto>()
            })
            .ToList();
        var combined = directory
            .Where(d => d.TechnicianUserId != excludeUserId)
            .Concat(extra)
            .OrderBy(r => r.Name)
            .ToList();
        return combined;
    }

    public async Task<TechnicianResponse> CreateTechnicianAsync(TechnicianCreateRequest request)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        var isSupervisor = request.IsSupervisor ||
                           string.Equals(request.Role, "SupervisorTechnician", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.");
        }
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ArgumentException("Full name is required.");
        }
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Password is required.");
        }
        if (request.Password != request.ConfirmPassword)
        {
            throw new ArgumentException("Password and confirmation do not match.");
        }
        if (!IsPasswordValid(request.Password))
        {
            throw new ArgumentException("Password must be at least 8 characters and include at least one letter and one number.");
        }

        if (await _userRepository.ExistsByEmailAsync(normalizedEmail))
        {
            throw new DuplicateEmailException("Email address is already registered.");
        }

        var technicians = await _technicianRepository.GetAllAsync();
        if (technicians.Any(t => t.Email != null && t.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DuplicateEmailException("Email address is already registered.");
        }
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var users = await _userRepository.GetAllAsync();
            if (users.Any(u => !string.IsNullOrWhiteSpace(u.PhoneNumber) && u.PhoneNumber == request.Phone))
            {
                throw new DuplicatePhoneException("Phone number is already registered.");
            }
            if (technicians.Any(t => !string.IsNullOrWhiteSpace(t.Phone) && t.Phone == request.Phone))
            {
                throw new DuplicatePhoneException("Phone number is already registered.");
            }
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = normalizedEmail,
            Role = UserRole.Technician,
            PhoneNumber = request.Phone,
            Department = request.Department,
            CreatedAt = DateTime.UtcNow,
            LockoutEnabled = false,
            LockoutEnd = null
        };
        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, request.Password);
        await _userRepository.AddAsync(newUser);

        var coverageSubcategoryIds = request.Coverage?
            .Select(c => c.SubcategoryId)
            .Distinct()
            .ToList();
        var subcategoryIds = coverageSubcategoryIds?.Count > 0
            ? coverageSubcategoryIds
            : request.SubcategoryIds;

        var technician = new Technician
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = normalizedEmail,
            Phone = request.Phone,
            Department = request.Department,
            IsActive = request.IsActive,
            IsSupervisor = isSupervisor,
            CreatedAt = DateTime.UtcNow,
            UserId = newUser.Id
        };

        await _technicianRepository.AddAsync(technician);
        await _unitOfWork.SaveChangesAsync();

        // Set subcategory permissions if provided
        if (subcategoryIds != null && subcategoryIds.Any())
        {
            // Validate subcategory IDs exist
            var allSubcategories = await _categoryRepository.GetAllAsync();
            var subcategoryToCategory = allSubcategories
                .SelectMany(c => c.Subcategories.Select(s => new { CategoryId = c.Id, SubcategoryId = s.Id }))
                .ToDictionary(x => x.SubcategoryId, x => x.CategoryId);
            var validSubcategoryIds = subcategoryToCategory.Keys.ToHashSet();
            
            var invalidIds = subcategoryIds.Where(id => !validSubcategoryIds.Contains(id)).ToList();
            if (invalidIds.Any())
            {
                throw new ArgumentException($"Invalid subcategory IDs: {string.Join(", ", invalidIds)}");
            }
            if (request.Coverage != null && request.Coverage.Any())
            {
                var mismatches = request.Coverage
                    .Where(c => subcategoryToCategory.TryGetValue(c.SubcategoryId, out var catId) && catId != c.CategoryId)
                    .Select(c => c.SubcategoryId)
                    .Distinct()
                    .ToList();
                if (mismatches.Any())
                {
                    throw new ArgumentException($"Coverage category mismatch for subcategories: {string.Join(", ", mismatches)}");
                }
            }

            await _unitOfWork.TechnicianSubcategoryPermissions.ReplacePermissionsAsync(
                technician.Id, 
                subcategoryIds);
            await _unitOfWork.SaveChangesAsync();
        }

        // Reload with includes for response
        var technicianWithIncludes = await _technicianRepository.GetByIdWithIncludesAsync(technician.Id);
        var response = MapToResponse(technicianWithIncludes ?? technician);
        return response;
    }

    public async Task<TechnicianResponse?> UpdateTechnicianAsync(Guid id, TechnicianUpdateRequest request)
    {
        var technician = await _technicianRepository.GetByIdAsync(id);

        if (technician == null)
        {
            return null;
        }

        technician.FullName = request.FullName;
        technician.Email = request.Email;
        technician.Phone = request.Phone;
        technician.Department = request.Department;
        technician.IsActive = request.IsActive; // Update IsActive status
        technician.IsSupervisor = request.IsSupervisor; // Update IsSupervisor status

        await _technicianRepository.UpdateAsync(technician);
        await _unitOfWork.SaveChangesAsync();

        // Update subcategory permissions if provided
        if (request.SubcategoryIds != null)
        {
            // Validate subcategory IDs exist
            var allSubcategories = await _categoryRepository.GetAllAsync();
            var validSubcategoryIds = allSubcategories
                .SelectMany(c => c.Subcategories)
                .Select(s => s.Id)
                .ToHashSet();
            
            var invalidIds = request.SubcategoryIds.Where(id => !validSubcategoryIds.Contains(id)).ToList();
            if (invalidIds.Any())
            {
                throw new ArgumentException($"Invalid subcategory IDs: {string.Join(", ", invalidIds)}");
            }

            await _unitOfWork.TechnicianSubcategoryPermissions.ReplacePermissionsAsync(
                id, 
                request.SubcategoryIds);
            await _unitOfWork.SaveChangesAsync();
        }

        // Reload with includes for response
        var technicianWithIncludes = await _technicianRepository.GetByIdWithIncludesAsync(id);
        return MapToResponse(technicianWithIncludes ?? technician);
    }

    public async Task<TechnicianResponse?> UpdateTechnicianExpertiseAsync(Guid id, List<int> subcategoryIds)
    {
        var technician = await _technicianRepository.GetByIdAsync(id);
        if (technician == null)
        {
            return null;
        }

        // Validate subcategory IDs exist
        var allSubcategories = await _categoryRepository.GetAllAsync();
        var validSubcategoryIds = allSubcategories
            .SelectMany(c => c.Subcategories)
            .Select(s => s.Id)
            .ToHashSet();

        var invalidIds = subcategoryIds.Where(subId => !validSubcategoryIds.Contains(subId)).ToList();
        if (invalidIds.Any())
        {
            throw new ArgumentException($"Invalid subcategory IDs: {string.Join(", ", invalidIds)}");
        }

        await _unitOfWork.TechnicianSubcategoryPermissions.ReplacePermissionsAsync(id, subcategoryIds);
        await _unitOfWork.SaveChangesAsync();

        var technicianWithIncludes = await _technicianRepository.GetByIdWithIncludesAsync(id);
        return MapToResponse(technicianWithIncludes ?? technician);
    }

    public async Task<bool> UpdateTechnicianStatusAsync(Guid id, bool isActive)
    {
        var technician = await _technicianRepository.GetByIdAsync(id);

        if (technician == null)
        {
            return false;
        }

        technician.IsActive = isActive;
        await _technicianRepository.UpdateAsync(technician);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    private static bool IsTerminalStatus(TicketStatus status)
    {
        return status == TicketStatus.Solved;
    }

    private static bool IsPasswordValid(string password)
    {
        if (password.Length < 8)
        {
            return false;
        }
        return System.Text.RegularExpressions.Regex.IsMatch(password, @"^(?=.*[a-zA-Z])(?=.*\d).+$");
    }

    public async Task<bool> IsTechnicianActiveAsync(Guid id)
    {
        var technician = await _technicianRepository.GetByIdAsync(id);
        return technician != null && technician.IsActive;
    }

    /// <summary>
    /// Links a Technician record to a User account (Admin-only operation)
    /// </summary>
    public async Task<(LinkUserResult result, TechnicianResponse? technician)> LinkUserAsync(Guid technicianId, Guid userId)
    {
        _logger.LogInformation("LinkUser: Attempting to link Technician {TechnicianId} to User {UserId}", technicianId, userId);

        var technician = await _technicianRepository.GetByIdAsync(technicianId);
        if (technician == null)
        {
            _logger.LogWarning("LinkUser FAILED: Technician {TechnicianId} not found", technicianId);
            return (LinkUserResult.TechnicianNotFound, null);
        }

        // Check if already linked
        if (technician.UserId != null)
        {
            _logger.LogWarning("LinkUser FAILED: Technician {TechnicianId} is already linked to User {ExistingUserId}", technicianId, technician.UserId);
            return (LinkUserResult.AlreadyLinked, null);
        }

        // Verify user exists and has Technician role
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            // Debug: Log all user IDs to help diagnose
            var allUsers = await _userRepository.GetAllAsync();
            var allUserIds = allUsers.Select(u => new { u.Id, u.Email, u.Role }).ToList();
            _logger.LogWarning("LinkUser FAILED: User {UserId} not found. Total users in DB: {Count}. Users: {@Users}", 
                userId, allUserIds.Count, allUserIds);
            return (LinkUserResult.UserNotFound, null);
        }

        if (user.Role != UserRole.Technician)
        {
            _logger.LogWarning("LinkUser FAILED: User {UserId} has role {Role}, expected Technician", userId, user.Role);
            return (LinkUserResult.UserNotTechnicianRole, null);
        }

        // Link technician to user
        technician.UserId = userId;
        await _technicianRepository.UpdateAsync(technician);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("LinkUser SUCCESS: Technician {TechnicianId} linked to User {UserId} ({UserEmail})", 
            technicianId, userId, user.Email);

        return (LinkUserResult.Success, MapToResponse(technician));
    }

    private static TechnicianResponse MapToResponse(Technician technician)
    {
        if (technician == null)
        {
            throw new ArgumentNullException(nameof(technician));
        }

        var subcategoryIds = technician.SubcategoryPermissions?
            .Where(p => p != null && p.SubcategoryId > 0)
            .Select(p => p.SubcategoryId)
            .Distinct()
            .ToList() ?? new List<int>();

        return new TechnicianResponse
        {
            Id = technician.Id,
            FullName = technician.FullName ?? string.Empty,
            Email = technician.Email ?? string.Empty,
            Phone = technician.Phone,
            Department = technician.Department,
            IsActive = technician.IsActive,
            IsSupervisor = technician.IsSupervisor,
            Role = technician.IsSupervisor ? "SupervisorTechnician" : "Technician",
            CreatedAt = technician.CreatedAt,
            UserId = technician.UserId,
            SubcategoryIds = subcategoryIds,
            CoverageCount = subcategoryIds.Count,
            // Soft delete fields
            IsDeleted = technician.IsDeleted,
            DeletedAt = technician.DeletedAt
        };
    }

    public async Task<TechnicianResponse?> GetTechnicianByUserIdAsync(Guid userId)
    {
        var technician = await _technicianRepository.GetByUserIdAsync(userId);
        return technician == null ? null : MapToResponse(technician);
    }

    public async Task<IEnumerable<TechnicianResponse>> GetAssignableTechniciansAsync()
    {
        // Return only active, non-supervisor technicians that can be assigned to tickets
        var technicians = await _technicianRepository.GetActiveWithUserIdAsync();
        return technicians
            .Where(t => !t.IsSupervisor && t.UserId != null) // Only normal technicians with linked users
            .Select(MapToResponse);
    }

    /// <summary>
    /// Soft deletes a technician:
    /// 1. Sets IsDeleted=true, DeletedAt=UtcNow, DeletedByUserId=adminUserId
    /// 2. Sets IsActive=false
    /// 3. Locks out the linked user account (prevents login)
    /// </summary>
    public async Task<(SoftDeleteResult result, TechnicianResponse? technician)> SoftDeleteTechnicianAsync(Guid technicianId, Guid adminUserId)
    {
        _logger.LogInformation("SoftDeleteTechnician: Attempting to soft delete Technician {TechnicianId} by Admin {AdminUserId}", 
            technicianId, adminUserId);

        // Need to bypass query filter to check if already deleted
        var technician = await _technicianRepository.GetByIdIncludingDeletedAsync(technicianId);
        if (technician == null)
        {
            _logger.LogWarning("SoftDeleteTechnician FAILED: Technician {TechnicianId} not found", technicianId);
            return (SoftDeleteResult.TechnicianNotFound, null);
        }

        // Check if already deleted (idempotent)
        if (technician.IsDeleted)
        {
            _logger.LogInformation("SoftDeleteTechnician: Technician {TechnicianId} is already deleted", technicianId);
            return (SoftDeleteResult.AlreadyDeleted, MapToResponse(technician));
        }

        try
        {
            // Mark as deleted
            technician.IsDeleted = true;
            technician.DeletedAt = DateTime.UtcNow;
            technician.DeletedByUserId = adminUserId;
            technician.IsActive = false; // Also deactivate

            await _technicianRepository.UpdateAsync(technician);

            // Lock out the linked user account to prevent login
            if (technician.UserId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(technician.UserId.Value);
                if (user != null)
                {
                    // Set lockout to far future to prevent login
                    user.LockoutEnd = DateTimeOffset.MaxValue;
                    user.LockoutEnabled = true;
                    // Invalidate existing sessions by updating security stamp
                    user.SecurityStamp = Guid.NewGuid().ToString();
                    await _userRepository.UpdateAsync(user);
                    
                    _logger.LogInformation("SoftDeleteTechnician: Locked out User {UserId} for deleted Technician {TechnicianId}", 
                        technician.UserId.Value, technicianId);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("SoftDeleteTechnician SUCCESS: Technician {TechnicianId} soft deleted by Admin {AdminUserId}", 
                technicianId, adminUserId);

            return (SoftDeleteResult.Success, MapToResponse(technician));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SoftDeleteTechnician FAILED: Error soft deleting Technician {TechnicianId}: {Message}", 
                technicianId, ex.Message);
            return (SoftDeleteResult.Failed, null);
        }
    }
}

public class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string message) : base(message) { }
}public class DuplicatePhoneException : Exception
{
    public DuplicatePhoneException(string message) : base(message) { }
}