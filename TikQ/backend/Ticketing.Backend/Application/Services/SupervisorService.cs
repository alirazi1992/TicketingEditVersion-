using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Services;

public class SupervisorService : ISupervisorService
{
    private readonly ITechnicianRepository _technicianRepository;
    private readonly ITechnicianService _technicianService;
    private readonly IUserRepository _userRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketTechnicianAssignmentRepository _assignmentRepository;
    private readonly ISupervisorTechnicianLinkRepository _linkRepository;
    private readonly ITicketActivityEventRepository _activityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SupervisorService> _logger;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public SupervisorService(
        ITechnicianRepository technicianRepository,
        ITechnicianService technicianService,
        IUserRepository userRepository,
        ITicketRepository ticketRepository,
        ITicketTechnicianAssignmentRepository assignmentRepository,
        ISupervisorTechnicianLinkRepository linkRepository,
        ITicketActivityEventRepository activityRepository,
        IUnitOfWork unitOfWork,
        ILogger<SupervisorService> logger,
        IHostEnvironment env,
        IConfiguration configuration)
    {
        _technicianRepository = technicianRepository;
        _technicianService = technicianService;
        _userRepository = userRepository;
        _ticketRepository = ticketRepository;
        _assignmentRepository = assignmentRepository;
        _linkRepository = linkRepository;
        _activityRepository = activityRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _env = env;
        _configuration = configuration;
    }

    private static bool _supervisorModeLoggedOnce;

    /// <summary>SupervisorTechnicians:Mode = "AllByDefault" | "LinkedOnly". If missing/empty and env.IsDevelopment() => AllByDefault.</summary>
    private string GetSupervisorMode()
    {
        var modeRaw = _configuration["SupervisorTechnicians:Mode"]?.Trim();
        var modeResolved = !string.IsNullOrEmpty(modeRaw) && string.Equals(modeRaw, "LinkedOnly", StringComparison.OrdinalIgnoreCase)
            ? "LinkedOnly"
            : (!string.IsNullOrEmpty(modeRaw) && string.Equals(modeRaw, "AllByDefault", StringComparison.OrdinalIgnoreCase))
                ? "AllByDefault"
                : (_env.IsDevelopment() ? "AllByDefault" : "LinkedOnly");

        if (_env.IsDevelopment() && !_supervisorModeLoggedOnce)
        {
            _supervisorModeLoggedOnce = true;
            _logger.LogInformation(
                "[SUPERVISOR_MODE] Environment={Env}, ModeResolved={ModeResolved}, SupervisorTechnicians:Mode(raw)={ModeRaw}",
                _env.EnvironmentName, modeResolved, modeRaw ?? "(null/empty)");
        }
        return modeResolved;
    }

    /// <summary>Single source of truth: same directory as Admin (TechnicianService), exclude current supervisor. No permission filtering.</summary>
    private async Task<List<TechnicianDirectoryItemDto>> GetActiveTechnicianDirectoryAsync(Guid excludeUserId)
    {
        var directory = await _technicianService.GetActiveTechnicianDirectoryForSupervisorAsync(excludeUserId);
        return directory.ToList();
    }

    public async Task<SupervisorTechniciansDiagnosticDto> GetSupervisorTechniciansDiagnosticAsync(Guid? supervisorUserId)
    {
        var excludeUserId = supervisorUserId ?? Guid.Empty;
        var directory = excludeUserId != Guid.Empty
            ? await GetActiveTechnicianDirectoryAsync(excludeUserId)
            : (await _technicianService.GetActiveTechnicianDirectoryForSupervisorAsync(null)).ToList();
        var activeTechCount = directory.Count;
        int linkedCount;
        List<Guid> sampleLinkedTechIds;
        if (supervisorUserId.HasValue && supervisorUserId.Value != Guid.Empty)
        {
            var links = (await _linkRepository.GetLinksForSupervisorAsync(supervisorUserId.Value)).ToList();
            linkedCount = links.Count;
            sampleLinkedTechIds = links.Select(l => l.TechnicianUserId).Distinct().Take(5).ToList();
        }
        else
        {
            linkedCount = await _linkRepository.GetTotalCountAsync();
            sampleLinkedTechIds = new List<Guid>();
        }

        return new SupervisorTechniciansDiagnosticDto
        {
            ActiveTechCount = activeTechCount,
            LinkedCount = linkedCount,
            SampleActiveTechEmails = directory.Select(d => d.Email).Where(e => !string.IsNullOrEmpty(e)).Take(5).ToList()!,
            SampleLinkedTechIds = sampleLinkedTechIds
        };
    }

    private async Task EnsureSupervisorAsync(Guid supervisorUserId)
    {
        var supervisor = await _technicianRepository.GetByUserIdAsync(supervisorUserId);
        if (supervisor == null || !supervisor.IsSupervisor)
        {
            throw new UnauthorizedAccessException("Only supervisor technicians can perform this action.");
        }
    }

    public async Task<IEnumerable<SupervisorTechnicianListItemDto>> GetTechniciansAsync(Guid supervisorUserId)
    {
        var supervisor = await _technicianRepository.GetByUserIdAsync(supervisorUserId);
        if (supervisor != null && !supervisor.IsSupervisor)
        {
            throw new UnauthorizedAccessException("Only supervisor technicians can perform this action.");
        }

        var mode = GetSupervisorMode();
        var activeDirectory = await GetActiveTechnicianDirectoryAsync(supervisorUserId);
        var activeTechCount = activeDirectory.Count;
        var links = await _linkRepository.GetLinksForSupervisorAsync(supervisorUserId);
        var linkedTechUserIds = links.Select(l => l.TechnicianUserId).Distinct().ToHashSet();
        var linkedCount = linkedTechUserIds.Count;

        List<TechnicianDirectoryItemDto> toReturn;
        if (mode == "LinkedOnly")
        {
            toReturn = activeDirectory.Where(d => linkedTechUserIds.Contains(d.TechnicianUserId)).ToList();
        }
        else
        {
            if (linkedCount > 0)
            {
                toReturn = activeDirectory.Where(d => linkedTechUserIds.Contains(d.TechnicianUserId)).ToList();
            }
            else
            {
                toReturn = activeDirectory;
            }
        }

        var results = toReturn.Select(d =>
        {
            var total = d.InboxTotalActive;
            var left = d.InboxLeftActiveNonTerminal;
            var percent = total == 0 ? 0 : (int)Math.Round((double)left / total * 100);
            return new SupervisorTechnicianListItemDto
            {
                TechnicianUserId = d.TechnicianUserId,
                TechnicianName = d.Name,
                InboxTotal = total,
                InboxLeft = left,
                WorkloadPercent = percent
            };
        }).OrderBy(r => r.TechnicianName).ToList();

        if (_env.IsDevelopment())
        {
            var supUser = await _userRepository.GetByIdAsync(supervisorUserId);
            _logger.LogInformation(
                "[SUPERVISOR_DEV] GetTechnicians: supervisorUserId={SupervisorUserId} email={Email} mode={Mode} activeTechCount={ActiveTechCount} linkedCount={LinkedCount} returnedCount={ReturnedCount}",
                supervisorUserId, supUser?.Email ?? "(null)", mode, activeTechCount, linkedCount, results.Count);
        }
        return results;
    }

    public async Task<IEnumerable<TechnicianResponse>> GetAvailableTechniciansAsync(Guid supervisorUserId)
    {
        var supervisor = await _technicianRepository.GetByUserIdAsync(supervisorUserId);
        if (supervisor != null && !supervisor.IsSupervisor)
        {
            throw new UnauthorizedAccessException("Only supervisor technicians can perform this action.");
        }

        // Directory = ActiveTechnicianDirectory (excluding current user). Same source for all supervisors.
        var directory = await _technicianService.GetActiveTechnicianDirectoryForSupervisorAsync(supervisorUserId);
        var activeDirectory = directory.ToList();
        var activeTechCount = activeDirectory.Count;

        // Linked = TechnicianUserId for this supervisor (userId consistently).
        var links = await _linkRepository.GetLinksForSupervisorAsync(supervisorUserId);
        var linkedIds = links.Select(l => l.TechnicianUserId).ToHashSet();
        var linkedCount = linkedIds.Count;

        // Available = directory minus linked (use directoryItem.TechnicianUserId, not Id).
        var directoryUserIds = activeDirectory.Select(d => d.TechnicianUserId).Distinct().ToList();
        var directoryTechsByUserId = directoryUserIds.Count > 0
            ? (await _technicianRepository.GetAllAsync())
                .Where(t => t.UserId.HasValue && directoryUserIds.Contains(t.UserId.Value))
                .ToDictionary(t => t.UserId!.Value)
            : new Dictionary<Guid, Technician>();
        var available = activeDirectory
            .Where(d => !linkedIds.Contains(d.TechnicianUserId) && !(directoryTechsByUserId.GetValueOrDefault(d.TechnicianUserId)?.IsSupervisor ?? false))
            .ToList();

        if (_env.IsDevelopment() && activeDirectory.Count > 0)
        {
            var first10Emails = activeDirectory.Select(d => d.Email).Where(e => !string.IsNullOrEmpty(e)).Take(10).ToList();
            _logger.LogInformation(
                "[SUPERVISOR_DEV] GetAvailableTechnicians directory (first 10 emails): {Emails}",
                string.Join(", ", first10Emails));
        }

        var userIds = available.Select(d => d.TechnicianUserId).Distinct().ToList();
        var users = userIds.Count > 0
            ? (await _userRepository.GetAllAsync()).Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id)
            : new Dictionary<Guid, User>();
        var techsByUserId = userIds.Count > 0
            ? (await _technicianRepository.GetAllAsync()).Where(t => t.UserId.HasValue && userIds.Contains(t.UserId.Value)).ToDictionary(t => t.UserId!.Value)
            : new Dictionary<Guid, Technician>();

        var results = available
            .Select(d =>
            {
                var user = users.GetValueOrDefault(d.TechnicianUserId);
                var tech = techsByUserId.GetValueOrDefault(d.TechnicianUserId);
                var createdAt = user?.CreatedAt;
                if (createdAt.HasValue && createdAt.Value == DateTime.MinValue)
                    createdAt = null;
                return new TechnicianResponse
                {
                    Id = d.TechnicianUserId,
                    UserId = d.TechnicianUserId,
                    FullName = d.Name,
                    Email = d.Email,
                    Phone = user?.PhoneNumber,
                    Department = d.Department ?? user?.Department,
                    IsActive = true,
                    IsSupervisor = tech?.IsSupervisor ?? false,
                    Role = (tech?.IsSupervisor ?? false) ? "SupervisorTechnician" : "Technician",
                    CreatedAt = createdAt,
                    SubcategoryIds = new List<int>(),
                    CoverageCount = 0
                };
            })
            .OrderBy(t => t.FullName)
            .ToList();

        if (_env.IsDevelopment())
        {
            var supUser = await _userRepository.GetByIdAsync(supervisorUserId);
            _logger.LogInformation(
                "[SUPERVISOR_DEV] GetAvailableTechnicians: supervisorUserId={SupervisorUserId} email={Email} directoryCount={DirectoryCount} linkedCount={LinkedCount} returnedCount={ReturnedCount}",
                supervisorUserId, supUser?.Email ?? "(null)", activeTechCount, linkedCount, results.Count);
            var minValueCount = results.Count(r => !r.CreatedAt.HasValue || r.CreatedAt.Value == DateTime.MinValue);
            var idMismatchCount = results.Count(r => r.Id != r.UserId);
            var knownSupervisorEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "supervisor@test.com", "techsuper@email.com" };
            var supervisorWrongFlagCount = results.Count(r => knownSupervisorEmails.Contains(r.Email ?? "") && !r.IsSupervisor);
            if (minValueCount > 0 || idMismatchCount > 0 || supervisorWrongFlagCount > 0)
            {
                _logger.LogWarning(
                    "[SUPERVISOR_DEV] GetAvailableTechnicians validation: createdAtMinValue={MinValueCount} idNeUserId={IdMismatchCount} knownSupervisorIsSupervisorFalse={SupervisorWrong}",
                    minValueCount, idMismatchCount, supervisorWrongFlagCount);
            }
        }
        return results;
    }

    public async Task<SupervisorTechnicianSummaryDto?> GetTechnicianSummaryAsync(Guid supervisorUserId, Guid technicianUserId)
    {
        await EnsureSupervisorAsync(supervisorUserId);
        var mode = GetSupervisorMode();
        var isLinked = await _linkRepository.IsLinkedAsync(supervisorUserId, technicianUserId);
        if (!isLinked && mode != "AllByDefault")
        {
            return null;
        }
        if (!isLinked && mode == "AllByDefault")
        {
            var directory = await GetActiveTechnicianDirectoryAsync(supervisorUserId);
            if (directory.All(d => d.TechnicianUserId != technicianUserId))
            {
                return null;
            }
        }

        var technicianUser = await _userRepository.GetByIdAsync(technicianUserId);
        if (technicianUser == null)
        {
            return null;
        }

        var allAssignments = await _assignmentRepository.GetTicketsForTechnicianAsync(technicianUserId);
        var activeAssignments = await _assignmentRepository.GetActiveTicketsForTechnicianAsync(technicianUserId);

        var archiveTickets = allAssignments
            .Where(a => a.Ticket != null && a.Ticket.Status == TicketStatus.Solved)
            .Select(a => MapTicketSummary(a.Ticket!))
            .DistinctBy(t => t.Id)
            .ToList();

        var activeTickets = activeAssignments
            .Where(a => a.Ticket != null && a.Ticket.Status != TicketStatus.Solved)
            .Select(a => MapTicketSummary(a.Ticket!))
            .DistinctBy(t => t.Id)
            .ToList();

        return new SupervisorTechnicianSummaryDto
        {
            TechnicianUserId = technicianUserId,
            TechnicianName = technicianUser.FullName,
            TechnicianEmail = technicianUser.Email,
            ArchiveTickets = archiveTickets,
            ActiveTickets = activeTickets
        };
    }

    public async Task<List<TicketSummaryDto>> GetAvailableTicketsAsync(Guid supervisorUserId)
    {
        await EnsureSupervisorAsync(supervisorUserId);
        var assignments = await _assignmentRepository.GetActiveTicketsForTechnicianAsync(supervisorUserId);
        return assignments
            .Where(a => a.Ticket != null && a.Ticket.Status != TicketStatus.Solved)
            .Select(a => MapTicketSummary(a.Ticket!))
            .DistinctBy(t => t.Id)
            .ToList();
    }

    public async Task<bool> LinkTechnicianAsync(Guid supervisorUserId, Guid technicianUserId)
    {
        // Allow both supervisors and admins (no Technician record) to link technicians
        var supervisor = await _technicianRepository.GetByUserIdAsync(supervisorUserId);
        if (supervisor != null && !supervisor.IsSupervisor)
        {
            throw new UnauthorizedAccessException("Only supervisor technicians can perform this action.");
        }

        var technician = await _technicianRepository.GetByUserIdAsync(technicianUserId);
        if (technician != null && technician.IsSupervisor)
        {
            throw new InvalidOperationException("Cannot link a supervisor as a technician.");
        }

        // If user has no Technician record (e.g. list built from User table), create one so linking works
        if (technician == null)
        {
            var user = await _userRepository.GetByIdAsync(technicianUserId);
            if (user == null || user.Role != UserRole.Technician)
            {
                throw new InvalidOperationException("Technician not found or user is not a technician.");
            }
            technician = new Technician
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.PhoneNumber,
                Department = user.Department,
                IsActive = true,
                IsSupervisor = false,
                CreatedAt = DateTime.UtcNow
            };
            await _technicianRepository.AddAsync(technician);
            await _unitOfWork.SaveChangesAsync();
        }

        if (await _linkRepository.IsLinkedAsync(supervisorUserId, technicianUserId))
        {
            return true;
        }

        await _linkRepository.AddAsync(new SupervisorTechnicianLink
        {
            SupervisorUserId = supervisorUserId,
            TechnicianUserId = technicianUserId,
            CreatedAt = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UnlinkTechnicianAsync(Guid supervisorUserId, Guid technicianUserId)
    {
        var supervisor = await _technicianRepository.GetByUserIdAsync(supervisorUserId);
        if (supervisor != null && !supervisor.IsSupervisor)
        {
            throw new UnauthorizedAccessException("Only supervisor technicians can perform this action.");
        }
        return await _linkRepository.RemoveAsync(supervisorUserId, technicianUserId);
    }

    public async Task<bool> AssignTicketAsync(Guid supervisorUserId, Guid technicianUserId, Guid ticketId)
    {
        await EnsureSupervisorAsync(supervisorUserId);
        if (!await _linkRepository.IsLinkedAsync(supervisorUserId, technicianUserId))
        {
            return false;
        }

        var supervisorAssignment = await _assignmentRepository.GetActiveAssignmentAsync(ticketId, supervisorUserId);
        if (supervisorAssignment == null)
        {
            return false;
        }

        var existingAssignment = await _assignmentRepository.GetActiveAssignmentAsync(ticketId, technicianUserId);
        if (existingAssignment != null)
        {
            return true;
        }

        var assignment = new TicketTechnicianAssignment
        {
            TicketId = ticketId,
            TechnicianUserId = technicianUserId,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = supervisorUserId,
            IsActive = true,
            Role = "Collaborator"
        };
        await _assignmentRepository.AddAsync(assignment);

        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return false;
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await _ticketRepository.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        await _activityRepository.AddEventAsync(
            ticketId,
            supervisorUserId,
            "Supervisor",
            "SupervisorAssigned",
            null,
            ticket.Status.ToString(),
            System.Text.Json.JsonSerializer.Serialize(new { technicianUserId }));

        return true;
    }

    public async Task<bool> RemoveAssignmentAsync(Guid supervisorUserId, Guid technicianUserId, Guid ticketId)
    {
        await EnsureSupervisorAsync(supervisorUserId);
        if (!await _linkRepository.IsLinkedAsync(supervisorUserId, technicianUserId))
        {
            return false;
        }

        var assignment = await _assignmentRepository.GetActiveAssignmentAsync(ticketId, technicianUserId);
        if (assignment == null || assignment.AssignedByUserId != supervisorUserId)
        {
            return false;
        }

        assignment.IsActive = false;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignmentRepository.UpdateAsync(assignment);

        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return false;
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await _ticketRepository.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        await _activityRepository.AddEventAsync(
            ticketId,
            supervisorUserId,
            "Supervisor",
            "SupervisorUnassigned",
            null,
            ticket.Status.ToString(),
            System.Text.Json.JsonSerializer.Serialize(new { technicianUserId }));

        return true;
    }

    private static TicketSummaryDto MapTicketSummary(Ticket ticket)
    {
        // Supervisors are technicians - they can see all statuses including Redo
        return new TicketSummaryDto
        {
            Id = ticket.Id,
            Title = ticket.Title,
            CanonicalStatus = ticket.Status,
            DisplayStatus = StatusMappingService.MapStatusForRole(ticket.Status, UserRole.Technician),
            ClientName = ticket.CreatedByUser?.FullName ?? string.Empty,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt
        };
    }

}

