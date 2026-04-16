using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ticketing.Backend.Application.DTOs;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;
namespace Ticketing.Backend.Application.Services;

/// <summary>
/// Thrown when a user attempts a status change they don't have permission for
/// </summary>
public class StatusChangeForbiddenException : Exception
{
    public StatusChangeForbiddenException(string message) : base(message) { }
}

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketMessageRepository _ticketMessageRepository;
    private readonly ITechnicianRepository _technicianRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITechnicianService _technicianService;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly ISmartAssignmentService _smartAssignmentService;
    private readonly ITicketTechnicianAssignmentRepository _assignmentRepository;
    private readonly ITicketActivityEventRepository _activityEventRepository;
    private readonly ITicketUserStateRepository _ticketUserStateRepository;
    private readonly ISupervisorTechnicianLinkRepository _supervisorLinkRepository;
    private readonly ITicketHubService? _ticketHubService;
    private readonly ILogger<TicketService>? _logger;

    private sealed class TicketAccessResult
    {
        public bool CanView { get; init; }
        public bool CanReply { get; init; }
        public bool CanEdit { get; init; }
        public bool IsReadOnly { get; init; }
        public string? ReadOnlyReason { get; init; }
        public bool CanGrantAccess { get; init; }
        /// <summary>Owner | Collaborator | Candidate | None</summary>
        public string AccessMode { get; init; } = "None";
        public bool CanAct { get; init; }
        public bool IsFaded { get; init; }
    }

    public TicketService(
        ITicketRepository ticketRepository,
        ITicketMessageRepository ticketMessageRepository,
        ITechnicianRepository technicianRepository,
        IUserRepository userRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork,
        ITechnicianService technicianService,
        ISystemSettingsService systemSettingsService,
        ISmartAssignmentService smartAssignmentService,
        ITicketTechnicianAssignmentRepository assignmentRepository,
        ITicketActivityEventRepository activityEventRepository,
        ITicketUserStateRepository ticketUserStateRepository,
        ISupervisorTechnicianLinkRepository supervisorLinkRepository,
        ITicketHubService? ticketHubService = null,
        ILogger<TicketService>? logger = null)
    {
        _ticketRepository = ticketRepository;
        _ticketMessageRepository = ticketMessageRepository;
        _technicianRepository = technicianRepository;
        _userRepository = userRepository;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
        _technicianService = technicianService;
        _systemSettingsService = systemSettingsService;
        _smartAssignmentService = smartAssignmentService;
        _assignmentRepository = assignmentRepository;
        _activityEventRepository = activityEventRepository;
        _ticketUserStateRepository = ticketUserStateRepository;
        _supervisorLinkRepository = supervisorLinkRepository;
        _ticketHubService = ticketHubService;
        _logger = logger;
    }

    public async Task<IEnumerable<TicketListItemResponse>> GetTicketsAsync(Guid userId, UserRole role, TicketStatus? status, TicketPriority? priority, Guid? assignedTo, Guid? createdBy, string? search, bool? unseen = null)
    {
        try
        {
            _logger?.LogInformation("GetTicketsAsync: UserId={UserId}, Role={Role}, Status={Status}, Priority={Priority}",
                userId, role, status, priority);

            List<TicketListItemResponse> tickets;
            try
            {
                tickets = await _ticketRepository.QueryListItemsAsync(
                    role,
                    userId,
                    status,
                    priority,
                    assignedTo,
                    createdBy,
                    search,
                    unseen);
                _logger?.LogInformation("GetTicketsAsync: QueryListItemsAsync completed successfully");
            }
            catch (Exception queryEx)
            {
                _logger?.LogError(queryEx, "GetTicketsAsync: QueryListItemsAsync FAILED - ExceptionType={ExceptionType}, Message={Message}, StackTrace={StackTrace}",
                    queryEx.GetType().Name, queryEx.Message, queryEx.StackTrace);
                throw;
            }

            if (tickets.Count == 0)
            {
                _logger?.LogInformation("GetTicketsAsync: No tickets found, returning empty list");
                return tickets;
            }

            foreach (var item in tickets)
            {
                item.DisplayStatus = StatusMappingService.MapStatusForRole(item.CanonicalStatus, role);
                item.IsUnread = item.IsUnseen;
                if (!string.IsNullOrWhiteSpace(item.LastMessagePreview) && item.LastMessagePreview.Length > 200)
                {
                    item.LastMessagePreview = item.LastMessagePreview.Substring(0, 200);
                }
            }

            // Enrich with acceptance info: only true when at least one assignment has AcceptedAt (do not treat Assigned as Accepted)
            var ticketIds = tickets.Select(t => t.Id).ToList();
            var acceptedInfo = await _assignmentRepository.GetFirstAcceptedByTicketIdsAsync(ticketIds);
            var acceptedByTicket = acceptedInfo.ToDictionary(x => x.TicketId, x => (x.AcceptedAt, x.AcceptedByUserId));
            foreach (var item in tickets)
            {
                if (acceptedByTicket.TryGetValue(item.Id, out var accepted))
                {
                    item.IsAccepted = true;
                    item.AcceptedAt = accepted.AcceptedAt;
                    item.AcceptedByUserId = accepted.AcceptedByUserId;
                }
                else
                {
                    item.IsAccepted = false;
                    item.AcceptedAt = null;
                    item.AcceptedByUserId = null;
                }
            }

            // Enrich list with accessMode/canAct/isFaded for technicians
            if (role == UserRole.Technician && tickets.Count > 0)
            {
                var technicianAssignments = (await _assignmentRepository.GetActiveTicketsForTechnicianAsync(userId)).ToList();
                var ticketIdToAssignment = technicianAssignments.ToDictionary(a => a.TicketId, a => a);
                foreach (var item in tickets)
                {
                    var isOwner = item.AssignedToUserId == userId;
                    if (isOwner)
                    {
                        item.AccessMode = "Owner";
                        item.CanAct = true;
                        item.IsFaded = false;
                        continue;
                    }
                    if (ticketIdToAssignment.TryGetValue(item.Id, out var assignment))
                    {
                        if (string.Equals(assignment.Role, "Collaborator", StringComparison.OrdinalIgnoreCase))
                        {
                            item.AccessMode = "Collaborator";
                            item.CanAct = true;
                            item.IsFaded = false;
                        }
                        else
                        {
                            item.AccessMode = "Candidate";
                            item.CanAct = item.AssignedToUserId == null;
                            item.IsFaded = item.AssignedToUserId != null;
                        }
                    }
                    else
                    {
                        item.AccessMode = "None";
                        item.CanAct = false;
                        item.IsFaded = true;
                    }
                }
            }

            _logger?.LogInformation("GetTicketsAsync: Successfully mapped {Count} tickets", tickets.Count);

            if (tickets.Count > 0)
            {
                var firstTicket = tickets[0];
                _logger?.LogDebug("GetTicketsAsync: First ticket - Id={Id}, Title={Title}, Status={Status}",
                    firstTicket.Id, firstTicket.Title, firstTicket.DisplayStatus);
            }

            return tickets;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "GetTicketsAsync FAILED: UserId={UserId}, Role={Role}, ExceptionType={ExceptionType}, Message={Message}, StackTrace={StackTrace}",
                userId, role, ex.GetType().Name, ex.Message, ex.StackTrace);
            throw;
        }
    }

    public async Task<IEnumerable<TicketCalendarResponse>> GetCalendarTicketsAsync(DateTime startDate, DateTime endDate, TicketStatus? status = null)
    {
        var tickets = await _ticketRepository.GetCalendarTicketsAsync(startDate, endDate, status);
        return tickets.Select(ticket => new TicketCalendarResponse
        {
            Id = ticket.Id,
            TicketNumber = ticket.Id.ToString(),
            Title = ticket.Title,
            CanonicalStatus = ticket.Status,
            DisplayStatus = ticket.Status,
            Priority = ticket.Priority,
            CategoryName = ticket.Category?.Name ?? string.Empty,
            AssignedTechnicianName = ticket.AssignedToUser?.FullName ?? ticket.Technician?.FullName,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            DueDate = ticket.DueDate
        });
    }

    public async Task<AdminTicketListResponse> GetAdminTicketsAsync(DateTime startDate, DateTime endDate, int page, int pageSize)
    {
        var (items, totalCount) = await _ticketRepository.GetAdminTicketsByCreatedRangeAsync(startDate, endDate, page, pageSize);
        return new AdminTicketListResponse
        {
            Items = items.Select(MapAdminListItem).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminTicketListResponse> GetAdminTicketsByUpdatedRangeAsync(DateTime startUtc, DateTime endUtcExclusive, int page, int pageSize)
    {
        var (items, totalCount) = await _ticketRepository.GetAdminTicketsByUpdatedRangeAsync(startUtc, endUtcExclusive, page, pageSize);
        return new AdminTicketListResponse
        {
            Items = items.Select(MapAdminListItem).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminTicketListResponse> GetAdminArchiveTicketsAsync(DateTime beforeDate, int page, int pageSize)
    {
        var (items, totalCount) = await _ticketRepository.GetAdminTicketsBeforeAsync(beforeDate, page, pageSize);
        return new AdminTicketListResponse
        {
            Items = items.Select(MapAdminListItem).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminTicketDetailsDto?> GetAdminTicketDetailsAsync(Guid ticketId)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return null;
        }

        var messages = ticket.Messages
            .OrderBy(m => m.CreatedAt)
            .ToList();

        var responders = new Dictionary<Guid, AdminTicketResponderDto>();
        foreach (var message in messages)
        {
            if (message.AuthorUser == null)
            {
                continue;
            }
            var roleLabel = await GetUserRoleLabelAsync(message.AuthorUser);
            if (roleLabel == "Client")
            {
                continue;
            }
            if (!responders.ContainsKey(message.AuthorUserId))
            {
                responders[message.AuthorUserId] = new AdminTicketResponderDto
                {
                    UserId = message.AuthorUserId,
                    Name = message.AuthorUser.FullName,
                    Role = roleLabel
                };
            }
        }

        var firstResponse = messages
            .FirstOrDefault(m => m.AuthorUser != null && m.AuthorUser.Role != UserRole.Client);

        var solvedAtEvent = ticket.ActivityEvents?
            .Where(ae => string.Equals(ae.NewStatus, TicketStatus.Solved.ToString(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(ae => ae.CreatedAt)
            .Select(ae => (DateTime?)ae.CreatedAt)
            .FirstOrDefault();

        var closedAt = GetClosedAt(ticket);

        var lastActivityAt = ticket.ActivityEvents?
            .OrderByDescending(ae => ae.CreatedAt)
            .Select(ae => (DateTime?)ae.CreatedAt)
            .FirstOrDefault() ?? ticket.UpdatedAt ?? ticket.CreatedAt;

        return new AdminTicketDetailsDto
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            CategoryName = ticket.Category?.Name ?? string.Empty,
            SubcategoryName = ticket.Subcategory?.Name,
            // Admin view - canonical and display are the same
            CanonicalStatus = ticket.Status,
            DisplayStatus = StatusMappingService.MapStatusForRole(ticket.Status, UserRole.Admin),
            CreatedAt = ticket.CreatedAt,
            ClosedAt = closedAt,
            LastActivityAt = lastActivityAt,
            TimeToFirstResponse = BuildDuration(ticket.CreatedAt, firstResponse?.CreatedAt),
            TimeToAnswered = BuildDuration(ticket.CreatedAt, solvedAtEvent),
            TimeToClosed = BuildDuration(ticket.CreatedAt, closedAt),
            ClientId = ticket.CreatedByUserId,
            ClientName = ticket.CreatedByUser?.FullName ?? string.Empty,
            ClientEmail = ticket.CreatedByUser?.Email ?? string.Empty,
            ClientPhone = ticket.CreatedByUser?.PhoneNumber,
            ClientDepartment = ticket.CreatedByUser?.Department,
            AssignedTechnicians = BuildAssignedTechnicians(ticket),
            Responders = responders.Values.ToList(),
            Messages = await MapAdminMessagesAsync(messages),
            ActivityEvents = ticket.ActivityEvents?
                .OrderByDescending(ae => ae.CreatedAt)
                .Select(ae => new TicketActivityEventDto
                {
                    Id = ae.Id,
                    TicketId = ae.TicketId,
                    ActorUserId = ae.ActorUserId,
                    ActorName = ae.ActorUser?.FullName ?? "Unknown",
                    ActorRole = ae.ActorRole,
                    EventType = ae.EventType,
                    OldStatus = ae.OldStatus,
                    NewStatus = ae.NewStatus,
                    MetadataJson = ae.MetadataJson,
                    CreatedAt = ae.CreatedAt
                })
                .ToList() ?? new List<TicketActivityEventDto>()
        };
    }

    public async Task<AdminTicketAssignmentResultDto?> AutoAssignTechniciansByCoverageAsync(Guid ticketId, Guid adminUserId)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return null;
        }

        var matchingTechnicians = await GetEligibleTechnicianUserIdsAsync(
            ticket.CategoryId,
            ticket.SubcategoryId);

        return await AddAssignmentsAsync(ticketId, adminUserId, matchingTechnicians, "AutoAssignedByAdmin");
    }

    public async Task<AdminTicketAssignmentResultDto?> ManualAssignTechniciansAsync(
        Guid ticketId,
        Guid adminUserId,
        List<Guid> technicianUserIds)
    {
        return await AddAssignmentsAsync(ticketId, adminUserId, technicianUserIds, "ManuallyAssignedByAdmin");
    }

    private async Task<AdminTicketAssignmentResultDto?> AddAssignmentsAsync(
        Guid ticketId,
        Guid adminUserId,
        IEnumerable<Guid> technicianUserIds,
        string eventType)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return null;
        }

        var desiredIds = technicianUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (!desiredIds.Any())
        {
            return await BuildAssignmentResultAsync(ticketId, new List<Guid>());
        }

        var existingAssignments = ticket.AssignedTechnicians?.ToList()
            ?? (await _assignmentRepository.GetAssignmentsForTicketAsync(ticketId)).ToList();

        var addedIds = new List<Guid>();

        foreach (var technicianUserId in desiredIds)
        {
            var user = await _userRepository.GetByIdAsync(technicianUserId);
            if (user == null || user.Role != UserRole.Technician)
            {
                continue;
            }

            var existing = existingAssignments.FirstOrDefault(a => a.TechnicianUserId == technicianUserId);
            if (existing != null)
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    existing.AssignedAt = DateTime.UtcNow;
                    existing.AssignedByUserId = adminUserId;
                    existing.Role = "Collaborator";
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _assignmentRepository.UpdateAsync(existing);
                    addedIds.Add(technicianUserId);
                }
                continue;
            }

            await _assignmentRepository.AddAsync(new TicketTechnicianAssignment
            {
                TicketId = ticketId,
                TechnicianUserId = technicianUserId,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = adminUserId,
                IsActive = true,
                Role = "Collaborator"
            });
            addedIds.Add(technicianUserId);
        }

        if (addedIds.Any())
        {
            if (ticket.Status == TicketStatus.Submitted)
            {
                ticket.Status = TicketStatus.Open;
            }

            ticket.UpdatedAt = DateTime.UtcNow;
            await _ticketRepository.UpdateAsync(ticket);
            await _unitOfWork.SaveChangesAsync();

            var metadata = System.Text.Json.JsonSerializer.Serialize(new { technicianUserIds = addedIds });
            await _activityEventRepository.AddEventAsync(
                ticketId,
                adminUserId,
                "Admin",
                eventType,
                null,
                null,
                metadata);

        }

        return await BuildAssignmentResultAsync(ticketId, addedIds);
    }

    private async Task<AdminTicketAssignmentResultDto> BuildAssignmentResultAsync(Guid ticketId, List<Guid> addedIds)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        var assignees = ticket?.AssignedTechnicians?
            .Where(a => a.IsActive && a.TechnicianUser != null)
            .Select(a => new AdminTicketAssigneeDto
            {
                TechnicianUserId = a.TechnicianUserId,
                Name = a.TechnicianUser?.FullName ?? "Unknown",
                Role = a.Role
            })
            .ToList() ?? new List<AdminTicketAssigneeDto>();

        var added = assignees.Where(a => addedIds.Contains(a.TechnicianUserId)).ToList();

        return new AdminTicketAssignmentResultDto
        {
            Assignees = assignees,
            AddedTechnicians = added
        };
    }

    private AdminTicketListItemDto MapAdminListItem(Ticket ticket)
    {
        var lastActivityAt = ticket.UpdatedAt ?? ticket.CreatedAt;
        var closedAt = GetClosedAt(ticket);

        return new AdminTicketListItemDto
        {
            Id = ticket.Id,
            Title = ticket.Title,
            CategoryName = ticket.Category?.Name ?? string.Empty,
            SubcategoryName = ticket.Subcategory?.Name,
            // Admin view - canonical and display are the same (admin sees all statuses)
            CanonicalStatus = ticket.Status,
            DisplayStatus = StatusMappingService.MapStatusForRole(ticket.Status, UserRole.Admin),
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt ?? ticket.CreatedAt,
            ClosedAt = closedAt,
            LastActivityAt = lastActivityAt,
            AssignedTechnicians = BuildAssignedTechnicians(ticket)
        };
    }

    private List<AdminTicketAssigneeDto> BuildAssignedTechnicians(Ticket ticket)
    {
        var assignments = ticket.AssignedTechnicians?
            .Where(ta => ta.IsActive)
            .Select(ta => new AdminTicketAssigneeDto
            {
                TechnicianUserId = ta.TechnicianUserId,
                Name = ta.TechnicianUser?.FullName ?? "Unknown",
                Role = ta.Role
            })
            .ToList() ?? new List<AdminTicketAssigneeDto>();

        if (assignments.Count == 0 && ticket.AssignedToUser != null)
        {
            assignments.Add(new AdminTicketAssigneeDto
            {
                TechnicianUserId = ticket.AssignedToUser.Id,
                Name = ticket.AssignedToUser.FullName,
                Role = null
            });
        }

        return assignments;
    }

    private async Task<List<AdminTicketMessageDto>> MapAdminMessagesAsync(List<TicketMessage> messages)
    {
        var results = new List<AdminTicketMessageDto>();
        foreach (var message in messages)
        {
            var authorRole = message.AuthorUser != null
                ? await GetUserRoleLabelAsync(message.AuthorUser)
                : "Unknown";

            results.Add(new AdminTicketMessageDto
            {
                Id = message.Id,
                AuthorName = message.AuthorUser?.FullName ?? "Unknown",
                AuthorRole = authorRole,
                Message = message.Message,
                CreatedAt = message.CreatedAt,
                Status = message.Status
            });
        }
        return results;
    }

    private static AdminTicketDurationDto? BuildDuration(DateTime start, DateTime? end)
    {
        if (!end.HasValue) return null;
        var duration = end.Value - start;
        if (duration.TotalSeconds < 0) return null;
        return new AdminTicketDurationDto
        {
            Seconds = duration.TotalSeconds,
            Display = FormatDuration(duration)
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
        {
            return "کمتر از یک دقیقه";
        }
        var parts = new List<string>();
        if (duration.Days > 0) parts.Add($"{duration.Days} روز");
        if (duration.Hours > 0) parts.Add($"{duration.Hours} ساعت");
        if (duration.Minutes > 0) parts.Add($"{duration.Minutes} دقیقه");
        return string.Join(" ", parts);
    }

    private DateTime? GetClosedAt(Ticket ticket)
    {
        var closedEvent = ticket.ActivityEvents?
            .Where(ae =>
                string.Equals(ae.NewStatus, "Closed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ae.NewStatus, TicketStatus.Solved.ToString(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ae.EventType, "Closed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(ae => ae.CreatedAt)
            .FirstOrDefault();

        if (closedEvent != null)
        {
            return closedEvent.CreatedAt;
        }

        if (ticket.Status == TicketStatus.Solved)
        {
            return ticket.UpdatedAt;
        }

        return null;
    }

    private async Task<string> GetUserRoleLabelAsync(User user)
    {
        if (user.Role == UserRole.Admin)
        {
            return "Admin";
        }
        if (user.Role == UserRole.Client)
        {
            return "Client";
        }
        if (user.Role == UserRole.Technician)
        {
            var technician = await _technicianRepository.GetByUserIdAsync(user.Id);
            return technician != null && technician.IsSupervisor ? "Supervisor" : "Technician";
        }
        return user.Role.ToString();
    }

    public async Task<TicketResponse?> GetTicketAsync(Guid id, Guid userId, UserRole role)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(id);

        if (ticket == null)
        {
            return null;
        }

        var access = await GetTicketAccessForLoadedTicketAsync(ticket, userId, role);
        if (!access.CanView)
        {
            return null;
        }

        // PHASE 2: SeenRead is a workflow status.
        // If a non-client actor (technician/admin) views a Submitted ticket, transition to SeenRead once.
        // Per-user read state is still tracked via TicketUserState.LastSeenAt (blue dot indicator).
        // Ancillary write: do not fail GET if status transition or save fails (idempotent best-effort).
        if (ticket.Status == TicketStatus.Submitted &&
            ticket.CreatedByUserId != userId &&
            (role == UserRole.Technician || role == UserRole.Admin))
        {
            try
            {
                var statusResult = await ChangeStatusAsync(ticket.Id, TicketStatus.SeenRead, userId, role);
                if (statusResult.Success && statusResult.Ticket != null)
                {
                    return statusResult.Ticket;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger?.LogWarning(ex, "GetTicketAsync: Ancillary status change (Submitted->SeenRead) failed, continuing with ticket load. Inner: {InnerMessage}", ex.InnerException?.Message ?? ex.Message);
            }
            catch (SqliteException ex)
            {
                _logger?.LogWarning(ex, "GetTicketAsync: Ancillary status change (Submitted->SeenRead) failed, continuing with ticket load. SqliteErrorCode: {Code}, Message: {Message}", ex.SqliteErrorCode, ex.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "GetTicketAsync: Ancillary status change failed, continuing with ticket load. Message: {Message}", ex.Message);
            }
        }

        var state = await _ticketUserStateRepository.GetStateAsync(ticket.Id, userId);
        var isFirstView = state == null || state.LastSeenAt == null;

        // Auto-mark as seen when technician/admin views the ticket (updates per-user state, not workflow status).
        // Idempotent best-effort: do not fail GET if upsert/activity save fails (e.g. unique constraint, FK).
        if (ticket.CreatedByUserId != userId && (role == UserRole.Technician || role == UserRole.Admin))
        {
            try
            {
                await _ticketUserStateRepository.UpsertSeenAsync(ticket.Id, userId, DateTime.UtcNow);
                await _unitOfWork.SaveChangesAsync();

                // Log activity event for first view (without changing status)
                if (isFirstView)
                {
                    var actor = await _userRepository.GetByIdAsync(userId);
                    var actorRole = await GetActorRoleLabelAsync(actor, role);
                    await _activityEventRepository.AddEventAsync(
                        ticket.Id,
                        userId,
                        actorRole,
                        "TechnicianOpened",
                        null, // No status change
                        null, // No status change
                        null);
                }

                // Refresh state after upsert
                state = await _ticketUserStateRepository.GetStateAsync(ticket.Id, userId);
            }
            catch (DbUpdateException ex)
            {
                _logger?.LogWarning(ex, "GetTicketAsync: Ancillary mark-seen/activity write failed, ticket still returned. Inner: {InnerMessage}", ex.InnerException?.Message ?? ex.Message);
            }
            catch (SqliteException ex)
            {
                _logger?.LogWarning(ex, "GetTicketAsync: Ancillary mark-seen/activity write failed, ticket still returned. SqliteErrorCode: {Code}, Message: {Message}", ex.SqliteErrorCode, ex.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "GetTicketAsync: Ancillary mark-seen/activity write failed, ticket still returned. Message: {Message}", ex.Message);
            }
        }

        var response = MapToResponse(ticket, userId, state?.LastSeenAt, role);
        response.CanView = access.CanView;
        response.CanReply = access.CanReply;
        response.CanEdit = access.CanEdit;
        response.IsReadOnly = access.IsReadOnly;
        response.ReadOnlyReason = access.ReadOnlyReason;
        response.CanGrantAccess = access.CanGrantAccess;
        response.AccessMode = access.AccessMode;
        response.CanAct = access.CanAct;
        response.IsFaded = access.IsFaded;
        if (role == UserRole.Technician)
        {
            var claimInfo = await GetClaimInfoAsync(ticket, userId);
            response.CanClaim = claimInfo.CanClaim;
            response.ClaimDisabledReason = claimInfo.Reason;
        }
        return response;
    }

    private async Task<TicketAccessResult> GetTicketAccessForLoadedTicketAsync(Ticket ticket, Guid userId, UserRole role)
    {
        // Admin: full access
        if (role == UserRole.Admin)
        {
            return new TicketAccessResult
            {
                CanView = true,
                CanReply = true,
                CanEdit = true,
                IsReadOnly = false,
                ReadOnlyReason = null,
                CanGrantAccess = true,
                AccessMode = "Owner",
                CanAct = true,
                IsFaded = false
            };
        }

        // Client: only their own ticket
        if (role == UserRole.Client)
        {
            var isOwner = ticket.CreatedByUserId == userId;
            return new TicketAccessResult
            {
                CanView = isOwner,
                CanReply = isOwner,
                CanEdit = isOwner,
                IsReadOnly = false,
                ReadOnlyReason = null,
                CanGrantAccess = false,
                AccessMode = isOwner ? "Owner" : "None",
                CanAct = isOwner,
                IsFaded = !isOwner
            };
        }

        // Technician rules
        if (role == UserRole.Technician)
        {
            var technician = await _technicianRepository.GetByUserIdAsync(userId);
            var isSupervisor = technician != null && technician.IsSupervisor;

            var isOwnerTechnician = ticket.AssignedToUserId == userId;
            var activeAssignment = ticket.AssignedTechnicians?
                .FirstOrDefault(a => a.IsActive && a.TechnicianUserId == userId);

            var isUnclaimed = ticket.AssignedToUserId == null;

            // Supervisor scope: can collaborate on tickets assigned to their team
            var supervisorScope = await CanSupervisorAccessTicketAsync(userId, ticket);

            // Determine eligibility for unclaimed tickets
            var eligibleForUnclaimed = false;
            if (isUnclaimed)
            {
                if (activeAssignment != null)
                {
                    eligibleForUnclaimed = true;
                }
                else
                {
                    var eligibleIds = await GetEligibleTechnicianUserIdsAsync(ticket.CategoryId, ticket.SubcategoryId);
                    eligibleForUnclaimed = eligibleIds.Contains(userId);
                }
            }

            // View rules
            var canView =
                isOwnerTechnician ||
                supervisorScope ||
                (activeAssignment != null) ||
                eligibleForUnclaimed;

            // Grant access: admin handled above, supervisor technicians only
            var canGrantAccess = isSupervisor;

            // Reply/Edit rules after claim
            if (!isUnclaimed)
            {
                if (isOwnerTechnician || supervisorScope)
                {
                    return new TicketAccessResult
                    {
                        CanView = canView,
                        CanReply = true,
                        CanEdit = true,
                        IsReadOnly = false,
                        ReadOnlyReason = null,
                        CanGrantAccess = canGrantAccess,
                        AccessMode = "Owner",
                        CanAct = true,
                        IsFaded = false
                    };
                }

                if (activeAssignment != null &&
                    string.Equals(activeAssignment.Role, "Collaborator", StringComparison.OrdinalIgnoreCase))
                {
                    return new TicketAccessResult
                    {
                        CanView = true,
                        CanReply = true,
                        CanEdit = true,
                        IsReadOnly = false,
                        ReadOnlyReason = null,
                        CanGrantAccess = canGrantAccess,
                        AccessMode = "Collaborator",
                        CanAct = true,
                        IsFaded = false
                    };
                }

                if (activeAssignment != null)
                {
                    return new TicketAccessResult
                    {
                        CanView = true,
                        CanReply = false,
                        CanEdit = false,
                        IsReadOnly = true,
                        ReadOnlyReason = "This ticket is already claimed by another technician. Ask a supervisor/admin to grant collaborator access.",
                        CanGrantAccess = canGrantAccess,
                        AccessMode = "Candidate",
                        CanAct = false,
                        IsFaded = true
                    };
                }

                return new TicketAccessResult
                {
                    CanView = false,
                    CanReply = false,
                    CanEdit = false,
                    IsReadOnly = true,
                    ReadOnlyReason = "You are not assigned to this ticket.",
                    CanGrantAccess = canGrantAccess,
                    AccessMode = "None",
                    CanAct = false,
                    IsFaded = true
                };
            }

            // Unclaimed ticket: Candidate can act (claim)
            if (eligibleForUnclaimed || activeAssignment != null || supervisorScope)
            {
                return new TicketAccessResult
                {
                    CanView = true,
                    CanReply = true,
                    CanEdit = false,
                    IsReadOnly = false,
                    ReadOnlyReason = null,
                    CanGrantAccess = canGrantAccess,
                    AccessMode = "Candidate",
                    CanAct = true,
                    IsFaded = false
                };
            }

            return new TicketAccessResult
            {
                CanView = false,
                CanReply = false,
                CanEdit = false,
                IsReadOnly = true,
                ReadOnlyReason = "You are not eligible to access this ticket.",
                CanGrantAccess = canGrantAccess,
                AccessMode = "None",
                CanAct = false,
                IsFaded = true
            };
        }

        return new TicketAccessResult
        {
            CanView = false,
            CanReply = false,
            CanEdit = false,
            IsReadOnly = true,
            ReadOnlyReason = "Unknown role.",
            CanGrantAccess = false,
            AccessMode = "None",
            CanAct = false,
            IsFaded = true
        };
    }

    /// <summary>
    /// Throws UnauthorizedAccessException("Ticket is read-only for you.") if the user is a Technician
    /// and does not have permission to perform write actions (reply or edit).
    /// Admin and TechSupervisor are always allowed. Technician must be Owner or Collaborator.
    /// </summary>
    private async Task EnsureTechnicianCanActAsync(Ticket ticket, Guid userId, UserRole role, bool requireReply)
    {
        if (role == UserRole.Admin) return;
        if (role == UserRole.Client) return;
        if (role != UserRole.Technician) return;
        var access = await GetTicketAccessForLoadedTicketAsync(ticket, userId, role);
        bool canAct = requireReply ? access.CanReply : access.CanEdit;
        if (!canAct)
        {
            throw new UnauthorizedAccessException("Ticket is read-only for you.");
        }
    }

    public async Task<bool> MarkTicketSeenAsync(Guid ticketId, Guid userId, UserRole role)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return false;
        }

        if (role == UserRole.Client && ticket.CreatedByUserId != userId)
        {
            return false;
        }

        if (role == UserRole.Technician)
        {
            var canView = await CanTechnicianViewTicketAsync(userId, ticket);
            if (!canView)
            {
                return false;
            }
        }

        await _ticketUserStateRepository.UpsertSeenAsync(ticketId, userId, DateTime.UtcNow);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<TicketResponse?> CreateTicketAsync(Guid userId, TicketCreateRequest request, List<DTOs.FileAttachmentRequest>? attachments = null)
    {
        _logger?.LogInformation("CreateTicketAsync: ENTERED - UserId={UserId}, CategoryId={CategoryId}, SubcategoryId={SubcategoryId}, Title={Title}",
            userId, request.CategoryId, request.SubcategoryId, request.Title);

        var requester = await _userRepository.GetByIdAsync(userId);
        if (requester == null)
        {
            throw new KeyNotFoundException("Requesting user not found.");
        }

        // Validate category & subcategory
        if (request.SubcategoryId == null)
        {
            throw new InvalidOperationException("Subcategory is required.");
        }

        var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
        if (category == null || !category.IsActive)
        {
            throw new InvalidOperationException("Invalid category selection.");
        }

        var subcategory = await _categoryRepository.GetSubcategoryByIdAsync(request.SubcategoryId.Value);
        if (subcategory == null || !subcategory.IsActive)
        {
            throw new InvalidOperationException("Invalid subcategory selection.");
        }

        if (subcategory.CategoryId != request.CategoryId)
        {
            throw new InvalidOperationException("Subcategory does not belong to the selected category.");
        }
        
        // Validate and load field definitions if subcategory and dynamic fields are provided
        var fieldDefinitions = new List<SubcategoryFieldDefinition>();
        if (request.SubcategoryId.HasValue)
        {
            fieldDefinitions = (await _unitOfWork.FieldDefinitions.GetBySubcategoryIdAsync(
                request.SubcategoryId.Value,
                includeInactive: false)).ToList();

            if (fieldDefinitions.Any())
            {
                var submittedFields = request.DynamicFields ?? new List<TicketDynamicFieldRequest>();

                var duplicateFieldIds = submittedFields
                    .GroupBy(field => field.FieldDefinitionId)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToList();
                if (duplicateFieldIds.Any())
                {
                    throw new InvalidOperationException($"Field validation failed: Duplicate field values submitted for field definitions {string.Join(", ", duplicateFieldIds)}");
                }

                // Validate field values
                var validationErrors = new List<string>();
                var resolvedFields = new List<(TicketDynamicFieldRequest FieldValue, SubcategoryFieldDefinition FieldDefinition)>();
                var fieldDefinitionsById = fieldDefinitions.ToDictionary(f => f.Id);
                var fieldDefinitionsByKey = fieldDefinitions
                    .GroupBy(f => f.FieldKey, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var fieldValue in submittedFields)
                {
                    if (fieldValue.FieldDefinitionId <= 0)
                    {
                        validationErrors.Add("Field definition id is invalid.");
                        continue;
                    }

                    if (!fieldDefinitionsById.TryGetValue(fieldValue.FieldDefinitionId, out var fieldDef))
                    {
                        var categoryField = await _unitOfWork.CategoryFieldDefinitions.GetByIdAsync(fieldValue.FieldDefinitionId);
                        if (categoryField == null)
                        {
                            validationErrors.Add($"Field definition {fieldValue.FieldDefinitionId} not found for this subcategory");
                            continue;
                        }
                        if (categoryField.CategoryId != request.CategoryId)
                        {
                            validationErrors.Add($"Field definition {fieldValue.FieldDefinitionId} does not belong to the selected category");
                            continue;
                        }
                        if (!categoryField.IsActive)
                        {
                            validationErrors.Add($"{categoryField.Label} is inactive");
                            continue;
                        }
                        if (!fieldDefinitionsByKey.TryGetValue(categoryField.Key, out fieldDef))
                        {
                            validationErrors.Add($"{categoryField.Label} is not available for the selected subcategory");
                            continue;
                        }
                    }

                    if (fieldDef == null)
                    {
                        validationErrors.Add($"Field definition {fieldValue.FieldDefinitionId} not found for this subcategory");
                        continue;
                    }

                    resolvedFields.Add((fieldValue, fieldDef));

                    // Check required fields
                    if (fieldDef.IsRequired && string.IsNullOrWhiteSpace(fieldValue.Value))
                    {
                        validationErrors.Add($"{fieldDef.Label} is required");
                    }

                    // Validate type-specific constraints
                    if (!string.IsNullOrWhiteSpace(fieldValue.Value))
                    {
                        if (fieldDef.Type == Domain.Enums.FieldType.Number)
                        {
                            if (!double.TryParse(fieldValue.Value, out var numValue))
                            {
                                validationErrors.Add($"{fieldDef.Label} must be a valid number");
                            }
                            else
                            {
                                if (fieldDef.Min.HasValue && numValue < fieldDef.Min.Value)
                                {
                                    validationErrors.Add($"{fieldDef.Label} must be at least {fieldDef.Min.Value}");
                                }
                                if (fieldDef.Max.HasValue && numValue > fieldDef.Max.Value)
                                {
                                    validationErrors.Add($"{fieldDef.Label} must be at most {fieldDef.Max.Value}");
                                }
                            }
                        }
                        else if (fieldDef.Type == Domain.Enums.FieldType.Select || fieldDef.Type == Domain.Enums.FieldType.MultiSelect)
                        {
                            if (!string.IsNullOrWhiteSpace(fieldDef.OptionsJson))
                            {
                                try
                                {
                                    var options = System.Text.Json.JsonSerializer.Deserialize<List<DTOs.FieldOption>>(fieldDef.OptionsJson);
                                    if (options != null && options.Any())
                                    {
                                        var validValues = options.Select(o => o.Value).ToList();
                                        var submittedValues = fieldValue.Value.Split(',').Select(v => v.Trim()).ToList();
                                        var invalidValues = submittedValues.Where(v => !validValues.Contains(v)).ToList();
                                        if (invalidValues.Any())
                                        {
                                            validationErrors.Add($"{fieldDef.Label} contains invalid values: {string.Join(", ", invalidValues)}");
                                        }
                                    }
                                }
                                catch
                                {
                                    // If options JSON is invalid, skip validation
                                }
                            }
                        }
                    }
                }

                var duplicateResolvedIds = resolvedFields
                    .GroupBy(f => f.FieldDefinition.Id)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                if (duplicateResolvedIds.Any())
                {
                    validationErrors.Add($"Duplicate field values submitted for field definitions {string.Join(", ", duplicateResolvedIds)}");
                }

                // Check for missing required fields
                var providedFieldIds = resolvedFields.Select(fv => fv.FieldDefinition.Id).ToHashSet();
                var missingRequired = fieldDefinitions
                    .Where(f => f.IsRequired && !providedFieldIds.Contains(f.Id))
                    .ToList();
                if (missingRequired.Any())
                {
                    validationErrors.AddRange(missingRequired.Select(f => $"{f.Label} is required"));
                }

                if (validationErrors.Any())
                {
                    throw new InvalidOperationException($"Field validation failed: {string.Join("; ", validationErrors)}");
                }
            }
        }
        
        // Clients create tickets for themselves; the role check happens in the controller
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
            Priority = request.Priority,
            Status = TicketStatus.Submitted,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _logger?.LogInformation("Creating ticket: UserId={UserId}, CategoryId={CategoryId}, SubcategoryId={SubcategoryId}, Title={Title}, Status={Status}",
            userId, request.CategoryId, request.SubcategoryId, request.Title, TicketStatus.Submitted);
        
        _logger?.LogInformation("CreateTicketAsync: About to save ticket to database - TicketId={TicketId}, CategoryId={CategoryId}, SubcategoryId={SubcategoryId}",
            ticket.Id, ticket.CategoryId, ticket.SubcategoryId);
        
        await _ticketRepository.AddAsync(ticket);
        
        try
        {
            _logger?.LogInformation("CreateTicketAsync: Calling SaveChangesAsync...");
            var saveResult = await _unitOfWork.SaveChangesAsync();
            _logger?.LogInformation("CreateTicketAsync: SaveChangesAsync COMPLETED - RowsAffected={RowsAffected}, TicketId={TicketId}, CreatedByUserId={CreatedByUserId}, Status={Status}, CategoryId={CategoryId}, SubcategoryId={SubcategoryId}",
                saveResult, ticket.Id, ticket.CreatedByUserId, ticket.Status, ticket.CategoryId, ticket.SubcategoryId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CreateTicketAsync: FAILED to save ticket to database. TicketId={TicketId}, CreatedByUserId={CreatedByUserId}, Exception={ExceptionType}, Message={Message}, StackTrace={StackTrace}",
                ticket.Id, ticket.CreatedByUserId, ex.GetType().Name, ex.Message, ex.StackTrace);
            throw; // Re-throw to let controller handle it
        }

        // Create activity event for ticket creation
        await _activityEventRepository.AddEventAsync(
            ticket.Id,
            userId,
            UserRole.Client.ToString(),
            "Created",
            null,
            ticket.Status.ToString(),
            null);

        // Save dynamic field values
        if (request.DynamicFields != null && request.DynamicFields.Any() && fieldDefinitions.Any())
        {
            // Reload ticket to ensure navigation properties are available
            ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticket.Id) ?? ticket;

            var fieldDefinitionsById = fieldDefinitions.ToDictionary(f => f.Id);
            var fieldDefinitionsByKey = fieldDefinitions
                .GroupBy(f => f.FieldKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var fieldValue in request.DynamicFields)
            {
                if (string.IsNullOrWhiteSpace(fieldValue.Value))
                {
                    continue;
                }

                if (!fieldDefinitionsById.TryGetValue(fieldValue.FieldDefinitionId, out var fieldDef))
                {
                    var categoryField = await _unitOfWork.CategoryFieldDefinitions.GetByIdAsync(fieldValue.FieldDefinitionId);
                    if (categoryField == null || categoryField.CategoryId != request.CategoryId)
                    {
                        continue;
                    }
                    if (!fieldDefinitionsByKey.TryGetValue(categoryField.Key, out fieldDef))
                    {
                        continue;
                    }
                }

                var ticketFieldValue = new Domain.Entities.TicketFieldValue
                {
                    Id = Guid.NewGuid(),
                    TicketId = ticket.Id,
                    FieldDefinitionId = fieldDef.Id,
                    Value = fieldValue.Value.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                ticket.FieldValues.Add(ticketFieldValue);
            }

            // Do not call UpdateAsync(ticket): we only added children; the ticket row is unchanged.
            // Marking the ticket Modified caused an UPDATE that affected 0 rows (concurrency exception).
            await _unitOfWork.SaveChangesAsync();
        }

        // ═══════════════════════════════════════════════════════════════════════════════════
        // DISPATCH: Notify and make visible to ALL eligible technicians (no single "pioneer")
        // ═══════════════════════════════════════════════════════════════════════════════════
        var eligibleTechnicianUserIds = await GetEligibleTechnicianUserIdsAsync(
            request.CategoryId,
            request.SubcategoryId);

        if (eligibleTechnicianUserIds.Any())
        {
            var isDev = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Development",
                StringComparison.OrdinalIgnoreCase);
            if (isDev)
            {
                _logger?.LogInformation(
                    "Dispatching ticket {TicketId} to {Count} eligible technicians (CategoryId={CategoryId}, SubcategoryId={SubcategoryId})",
                    ticket.Id, eligibleTechnicianUserIds.Count, request.CategoryId, request.SubcategoryId);
                _logger?.LogInformation(
                    "Eligible technicians for TicketId={TicketId}: [{TechnicianUserIds}]",
                    ticket.Id, string.Join(", ", eligibleTechnicianUserIds));
            }

            var newlyAssignedIds = await EnsureCandidateAssignmentsAsync(
                ticket.Id,
                userId,
                eligibleTechnicianUserIds);

            if (newlyAssignedIds.Any())
            {
                ticket.UpdatedAt = DateTime.UtcNow;
                await _ticketRepository.UpdateAsync(ticket);
                await _unitOfWork.SaveChangesAsync();

                var actorRole = requester.Role == UserRole.Admin ? "Admin" : "System";
                var metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    technicianUserIds = eligibleTechnicianUserIds,
                    categoryId = request.CategoryId,
                    subcategoryId = request.SubcategoryId,
                    autoAssigned = true
                });

                await _activityEventRepository.AddEventAsync(
                    ticket.Id,
                    userId,
                    actorRole,
                    "AssignedTechnicians",
                    null,
                    ticket.Status.ToString(),
                    metadata);

                await BroadcastAssignmentChangedAsync(ticket, newlyAssignedIds, userId);
            }
        }
        else
        {
            _logger?.LogInformation(
                "No eligible technicians found for TicketId={TicketId} (CategoryId={CategoryId}, SubcategoryId={SubcategoryId}).",
                ticket.Id, request.CategoryId, request.SubcategoryId);
        }

        // Handle attachments (optional - attachments are NOT required)
        if (attachments != null && attachments.Any())
        {
            try
            {
                _logger?.LogInformation("Processing {Count} attachments for ticket {TicketId}", attachments.Count, ticket.Id);
                
                // Reload ticket to ensure navigation properties are available
                ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticket.Id) ?? ticket;
                
                // For now, we'll store attachment metadata in the database
                // File storage can be implemented later if needed
                // Attachments are optional, so we'll just log them for now
                foreach (var attachment in attachments)
                {
                    _logger?.LogDebug("Attachment: {FileName}, Size: {FileSize} bytes, ContentType: {ContentType}",
                        attachment.FileName, attachment.FileSize, attachment.ContentType);
                    // TODO: Implement file storage if needed
                    // For now, attachments are accepted but not persisted (optional feature)
                }
                
                _logger?.LogInformation("Attachments processed for ticket {TicketId} (metadata only, file storage not implemented)", ticket.Id);
            }
            catch (Exception ex)
            {
                // Log but don't fail ticket creation if attachment handling fails
                _logger?.LogWarning(ex, "Failed to process attachments for ticket {TicketId}, but ticket was created successfully", ticket.Id);
            }
        }

        _logger?.LogInformation("CreateTicketAsync: Reloading ticket with includes before mapping - TicketId={TicketId}", ticket.Id);
        ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticket.Id);
        if (ticket == null)
        {
            _logger?.LogError("CreateTicketAsync: Ticket {TicketId} was created but could not be retrieved from database after creation", ticket?.Id ?? Guid.Empty);
            return null;
        }

        _logger?.LogInformation("CreateTicketAsync: About to map ticket to response - TicketId={TicketId}", ticket.Id);
        await _ticketUserStateRepository.UpsertSeenAsync(ticket.Id, userId, DateTime.UtcNow);
        await _unitOfWork.SaveChangesAsync();

        // Return via GetTicketAsync so access flags are included consistently
        var response = await GetTicketAsync(ticket.Id, userId, UserRole.Client);
        if (response != null)
        {
            _logger?.LogInformation("CreateTicketAsync: SUCCESS - Returning TicketResponse with Id={TicketId}, DisplayStatus={DisplayStatus}, CategoryId={CategoryId}, SubcategoryId={SubcategoryId}",
                response.Id, response.DisplayStatus, response.CategoryId, response.SubcategoryId);
        }
        return response;
    }

    private async Task<List<Guid>> GetEligibleTechnicianUserIdsAsync(int categoryId, int? subcategoryId)
    {
        IEnumerable<Guid> ids = Array.Empty<Guid>();

        if (subcategoryId.HasValue)
        {
            ids = await _unitOfWork.TechnicianSubcategoryPermissions
                .GetTechnicianUserIdsBySubcategoryIdAsync(subcategoryId.Value);
        }
        else
        {
            ids = await _unitOfWork.TechnicianSubcategoryPermissions
                .GetTechnicianUserIdsByCategoryIdAsync(categoryId);
        }

        return ids
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    private async Task<List<Guid>> EnsureCandidateAssignmentsAsync(
        Guid ticketId,
        Guid assignedByUserId,
        IEnumerable<Guid> eligibleTechnicianUserIds)
    {
        var desiredIds = eligibleTechnicianUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (!desiredIds.Any())
        {
            return new List<Guid>();
        }

        var existingAssignments = (await _assignmentRepository.GetAssignmentsForTicketAsync(ticketId)).ToList();
        var existingByUserId = existingAssignments.ToDictionary(a => a.TechnicianUserId, a => a);
        var newlyAssigned = new List<Guid>();

        foreach (var technicianUserId in desiredIds)
        {
            if (!existingByUserId.TryGetValue(technicianUserId, out var existing))
            {
                await _assignmentRepository.AddAsync(new TicketTechnicianAssignment
                {
                    TicketId = ticketId,
                    TechnicianUserId = technicianUserId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = assignedByUserId,
                    IsActive = true,
                    Role = "Candidate"
                });
                newlyAssigned.Add(technicianUserId);
                continue;
            }

            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.AssignedAt = DateTime.UtcNow;
                existing.AssignedByUserId = assignedByUserId;
                existing.Role = "Candidate";
                existing.UpdatedAt = DateTime.UtcNow;
                await _assignmentRepository.UpdateAsync(existing);
                newlyAssigned.Add(technicianUserId);
            }
        }

        return newlyAssigned;
    }

    private async Task<bool> CanTechnicianViewTicketAsync(Guid technicianUserId, Ticket ticket)
    {
        if (ticket.AssignedToUserId == technicianUserId)
        {
            return true;
        }

        var activeAssignment = ticket.AssignedTechnicians?
            .FirstOrDefault(ta => ta.TechnicianUserId == technicianUserId && ta.IsActive);
        if (activeAssignment != null)
        {
            // IMPORTANT: Candidates must still be able to VIEW after claim (read-only enforced elsewhere)
            return true;
        }

        if (ticket.AssignedToUserId == null)
        {
            var eligibleIds = await GetEligibleTechnicianUserIdsAsync(ticket.CategoryId, ticket.SubcategoryId);
            if (eligibleIds.Contains(technicianUserId))
            {
                return true;
            }
        }

        var technician = await _technicianRepository.GetByUserIdAsync(technicianUserId);
        if (technician != null && technician.IsSupervisor)
        {
            var linkedTechnicians = await _supervisorLinkRepository.GetLinksForSupervisorAsync(technicianUserId);
            var linkedIds = linkedTechnicians.Select(l => l.TechnicianUserId).ToHashSet();
            var hasTeamAssignment = ticket.AssignedTechnicians?.Any(ta => ta.IsActive && linkedIds.Contains(ta.TechnicianUserId)) ?? false;
            if (hasTeamAssignment)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<(bool CanClaim, string? Reason)> GetClaimInfoAsync(Ticket ticket, Guid technicianUserId)
    {
        if (ticket.AssignedToUserId != null)
        {
            return (false, "already claimed");
        }

        if (ticket.Status == TicketStatus.Solved)
        {
            return (false, "ticket closed");
        }

        var hasActiveAssignment = ticket.AssignedTechnicians?
            .Any(ta => ta.TechnicianUserId == technicianUserId && ta.IsActive) ?? false;

        var isEligibleByPermission = false;
        if (!hasActiveAssignment)
        {
            var eligibleIds = await GetEligibleTechnicianUserIdsAsync(ticket.CategoryId, ticket.SubcategoryId);
            isEligibleByPermission = eligibleIds.Contains(technicianUserId);
        }

        if (!hasActiveAssignment && !isEligibleByPermission)
        {
            return (false, "not eligible");
        }

        return (true, null);
    }

    public async Task<TicketResponse?> UpdateTicketAsync(Guid id, Guid userId, UserRole role, TicketUpdateRequest request)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null)
        {
            return null;
        }

        // Enforce centralized access rules (server-side)
        ticket = await _ticketRepository.GetByIdWithIncludesAsync(id);
        if (ticket == null) return null;
        var access = await GetTicketAccessForLoadedTicketAsync(ticket, userId, role);
        await EnsureTechnicianCanActAsync(ticket, userId, role, requireReply: false);
        if (!access.CanEdit)
        {
            throw new UnauthorizedAccessException("Ticket is read-only for you.");
        }

        // Handle non-status updates (description, priority, assignment, due date)
        bool hasNonStatusUpdates = false;
        
        if (request.Description != null && role != UserRole.Technician)
        {
            ticket.Description = request.Description;
            hasNonStatusUpdates = true;
        }

        if (request.Priority.HasValue && role != UserRole.Technician)
        {
            ticket.Priority = request.Priority.Value;
            hasNonStatusUpdates = true;
        }

        if (role == UserRole.Admin)
        {
            if (request.AssignedToUserId.HasValue)
            {
                ticket.AssignedToUserId = request.AssignedToUserId.Value;
                hasNonStatusUpdates = true;
            }
            if (request.DueDate != ticket.DueDate)
            {
                ticket.DueDate = request.DueDate;
                hasNonStatusUpdates = true;
            }
        }

        // Save non-status updates first
        if (hasNonStatusUpdates)
        {
            ticket.UpdatedAt = DateTime.UtcNow;
            await _ticketRepository.UpdateAsync(ticket);
            await _unitOfWork.SaveChangesAsync();
            var actorRoleLabel = await GetActorRoleLabelForTicketAsync(ticket, userId, role);
            await _activityEventRepository.AddEventAsync(id, userId, actorRoleLabel, "TicketUpdated", null, null, null);
        }

        // Handle status change via CENTRALIZED method (single source of truth)
        if (request.Status.HasValue)
        {
            var statusResult = await ChangeStatusAsync(id, request.Status.Value, userId, role);
            if (!statusResult.Success)
            {
                // Status change was forbidden - throw exception for proper error handling
                throw new StatusChangeForbiddenException(statusResult.ErrorMessage ?? "Status change not permitted");
            }
            // Return the updated ticket from status change
            return statusResult.Ticket;
        }

        return await GetTicketAsync(id, userId, role);
    }

    public async Task<TicketResponse?> AssignTicketAsync(Guid id, Guid technicianId)
    {
        var ticket = await _ticketRepository.GetByIdAsync(id);
        if (ticket == null)
        {
            return null;
        }

        // Load technician to get UserId (required for AssignedToUserId foreign key)
        var technician = await _technicianRepository.GetByIdAsync(technicianId);
        
        if (technician == null || !technician.IsActive)
        {
            return null; // Technician not found or inactive
        }

        // Set both TechnicianId (for display/navigation) and AssignedToUserId (for filtering/queries)
        ticket.TechnicianId = technicianId;
        ticket.AssignedToUserId = technician.UserId; // CRITICAL: Set to Technician.UserId (User.Id), not null
        // When assigning, set status to Open (not InProgress) - technician will change to InProgress when they start working
        ticket.Status = TicketStatus.Open;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _ticketRepository.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        return await GetTicketAsync(id, Guid.Empty, UserRole.Admin);
    }

    public async Task<TicketResponse?> ClaimTicketAsync(Guid ticketId, Guid technicianUserId)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return null;
        }

        var claimInfo = await GetClaimInfoAsync(ticket, technicianUserId);
        if (!claimInfo.CanClaim)
        {
            throw new InvalidOperationException(claimInfo.Reason ?? "Ticket cannot be claimed");
        }

        var technician = await _technicianRepository.GetByUserIdAsync(technicianUserId);
        if (technician == null || !technician.IsActive || technician.IsDeleted)
        {
            throw new InvalidOperationException("Technician not found or inactive");
        }

        var assignments = ticket.AssignedTechnicians?.ToList()
            ?? (await _assignmentRepository.GetAssignmentsForTicketAsync(ticketId)).ToList();

        var existing = assignments.FirstOrDefault(a => a.TechnicianUserId == technicianUserId);
        if (existing != null)
        {
            existing.IsActive = true;
            existing.AssignedAt = DateTime.UtcNow;
            existing.AssignedByUserId = technicianUserId;
            existing.Role = "Owner";
            existing.UpdatedAt = DateTime.UtcNow;
            await _assignmentRepository.UpdateAsync(existing);
        }
        else
        {
            await _assignmentRepository.AddAsync(new TicketTechnicianAssignment
            {
                TicketId = ticketId,
                TechnicianUserId = technicianUserId,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = technicianUserId,
                IsActive = true,
                Role = "Owner"
            });
        }

        // IMPORTANT: Do NOT deactivate other candidate assignments on claim.
        // Other technicians must remain able to view the ticket (read-only enforced by access policy),
        // and can be granted collaborator access later by supervisor/admin.

        ticket.AssignedToUserId = technicianUserId;
        ticket.TechnicianId = technician.Id;
        if (ticket.Status == TicketStatus.Submitted || ticket.Status == TicketStatus.SeenRead)
        {
            ticket.Status = TicketStatus.Open;
        }
        ticket.UpdatedAt = DateTime.UtcNow;
        await _ticketRepository.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        await _activityEventRepository.AddEventAsync(
            ticketId,
            technicianUserId,
            "Technician",
            "Assigned",
            null,
            ticket.Status.ToString(),
            System.Text.Json.JsonSerializer.Serialize(new { technicianUserId }));

        await BroadcastAssignmentChangedAsync(ticket, new List<Guid> { technicianUserId }, technicianUserId);

        return await GetTicketAsync(ticketId, technicianUserId, UserRole.Technician);
    }

    /// <summary>
    /// Read-only: returns messages for a ticket. Does NOT call GetTicketAsync (no status/seen/activity writes).
    /// </summary>
    public async Task<IEnumerable<TicketMessageDto>> GetMessagesAsync(Guid ticketId, Guid userId, UserRole role)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return Enumerable.Empty<TicketMessageDto>();
        }

        var access = await GetTicketAccessForLoadedTicketAsync(ticket, userId, role);
        if (!access.CanView)
        {
            return Enumerable.Empty<TicketMessageDto>();
        }

        var messages = await _ticketMessageRepository.GetByTicketIdAsync(ticketId);
        var list = new List<TicketMessageDto>();
        foreach (var m in messages)
        {
            var authorRole = "Unknown";
            if (m.AuthorUser != null)
                authorRole = m.AuthorUser.Role == UserRole.Technician ? "Technician" : m.AuthorUser.Role.ToString();
            list.Add(new TicketMessageDto
            {
                Id = m.Id,
                AuthorUserId = m.AuthorUserId,
                AuthorName = m.AuthorUser?.FullName ?? "Unknown",
                AuthorEmail = m.AuthorUser?.Email ?? string.Empty,
                AuthorRole = authorRole,
                Message = m.Message,
                CreatedAt = m.CreatedAt,
                Status = m.Status
            });
        }

#if DEBUG
        _logger?.LogInformation("GetMessagesAsync: ticketId={TicketId}, returned count={Count} (read-only, no SaveChanges)", ticketId, list.Count);
#endif
        return list;
    }

    public async Task<TicketMessageDto?> AddMessageAsync(Guid ticketId, Guid authorId, string message, TicketStatus? status = null)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            throw new KeyNotFoundException("Ticket not found");
        }

        var author = await _userRepository.GetByIdAsync(authorId);
        if (author == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Enforce centralized access rules (server-side)
        var access = await GetTicketAccessForLoadedTicketAsync(ticket, authorId, author.Role);
        await EnsureTechnicianCanActAsync(ticket, authorId, author.Role, requireReply: true);
        if (!access.CanReply)
        {
            throw new UnauthorizedAccessException("Ticket is read-only for you.");
        }

        // Handle status change via CENTRALIZED method (single source of truth)
        // This ensures all status changes go through one validated path
        var finalStatus = ticket.Status;
        if (status.HasValue)
        {
            var statusResult = await ChangeStatusAsync(ticketId, status.Value, authorId, author.Role);
            if (!statusResult.Success)
            {
                throw new StatusChangeForbiddenException(statusResult.ErrorMessage ?? "Status change not permitted");
            }
            finalStatus = statusResult.NewStatus ?? ticket.Status;
            
            // Reload ticket after status change
            ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
            if (ticket == null)
            {
                return null;
            }
        }
        else
        {
            // If no status change, still update UpdatedAt for the message
            ticket.UpdatedAt = DateTime.UtcNow;
            await _ticketRepository.UpdateAsync(ticket);
            await _unitOfWork.SaveChangesAsync();
        }

        // Create the message
        var ticketMessage = new TicketMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorUserId = authorId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            Status = finalStatus
        };

        await _ticketMessageRepository.AddAsync(ticketMessage);
        await _unitOfWork.SaveChangesAsync();

        // Create activity event for reply (Owner/Collaborator/Admin/Supervisor for timeline)
        var replyActorRole = await GetActorRoleLabelForTicketAsync(ticket, authorId, author.Role);
        await _activityEventRepository.AddEventAsync(
            ticketId,
            authorId,
            replyActorRole,
            "ReplyAdded",
            null,
            null,
            System.Text.Json.JsonSerializer.Serialize(new { messagePreview = message.Substring(0, Math.Min(100, message.Length)) }));

        var createdMessage = await _ticketMessageRepository.GetByIdWithAuthorAsync(ticketMessage.Id);
        if (createdMessage == null)
        {
            return null;
        }

        // PHASE 3: Broadcast reply via SignalR for real-time sync
        await BroadcastReplyAddedAsync(ticket, createdMessage, author);

        return new TicketMessageDto
        {
            Id = createdMessage.Id,
            AuthorUserId = createdMessage.AuthorUserId,
            AuthorName = createdMessage.AuthorUser!.FullName,
            AuthorEmail = createdMessage.AuthorUser.Email,
            Message = createdMessage.Message,
            CreatedAt = createdMessage.CreatedAt,
            Status = createdMessage.Status
        };
    }

    public async Task<TicketResponse?> UpdateCollaboratorAsync(Guid ticketId, Guid actorUserId, UserRole actorRole, Guid technicianUserId, string action)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return null;
        }

        // Authorization: Admin or Supervisor technician
        if (actorRole == UserRole.Technician)
        {
            var actorTech = await _technicianRepository.GetByUserIdAsync(actorUserId);
            if (actorTech == null || !actorTech.IsSupervisor)
            {
                throw new UnauthorizedAccessException("Only supervisor technicians can grant/revoke collaborator access.");
            }

            // Supervisor must be in-scope for this ticket (team assignment)
            var inScope = await CanSupervisorAccessTicketAsync(actorUserId, ticket);
            if (!inScope)
            {
                throw new UnauthorizedAccessException("Supervisor is not authorized for this ticket.");
            }
        }
        else if (actorRole != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Only admin or supervisor technicians can grant/revoke collaborator access.");
        }

        var targetUser = await _userRepository.GetByIdAsync(technicianUserId);
        if (targetUser == null || targetUser.Role != UserRole.Technician)
        {
            throw new ArgumentException("technicianUserId must be a valid Technician user.");
        }

        var normalizedAction = action.Trim().ToLowerInvariant();
        if (normalizedAction != "grant" && normalizedAction != "revoke")
        {
            throw new ArgumentException("action must be 'grant' or 'revoke'.");
        }

        var existing = ticket.AssignedTechnicians?
            .FirstOrDefault(a => a.TechnicianUserId == technicianUserId)
            ?? await _assignmentRepository.GetActiveAssignmentAsync(ticketId, technicianUserId);

        if (normalizedAction == "grant")
        {
            if (existing != null)
            {
                existing.IsActive = true;
                existing.Role = "Collaborator";
                existing.AssignedByUserId = actorUserId;
                existing.AssignedAt = existing.AssignedAt == default ? DateTime.UtcNow : existing.AssignedAt;
                existing.UpdatedAt = DateTime.UtcNow;
                await _assignmentRepository.UpdateAsync(existing);
            }
            else
            {
                await _assignmentRepository.AddAsync(new TicketTechnicianAssignment
                {
                    TicketId = ticketId,
                    TechnicianUserId = technicianUserId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = actorUserId,
                    IsActive = true,
                    Role = "Collaborator"
                });
            }
        }
        else // revoke
        {
            if (existing != null)
            {
                // Keep assignment active but revert to Candidate so technician can still view read-only
                existing.IsActive = true;
                existing.Role = "Candidate";
                existing.UpdatedAt = DateTime.UtcNow;
                await _assignmentRepository.UpdateAsync(existing);
            }
        }

        // Log activity for timeline (grant/revoke collaborator access)
        var actorForRole = await _userRepository.GetByIdAsync(actorUserId);
        var actorRoleLabel = await GetActorRoleLabelAsync(actorForRole, actorRole);
        var targetName = targetUser?.FullName ?? targetUser?.Email ?? technicianUserId.ToString();
        var eventType = normalizedAction == "grant" ? "AccessGranted" : "AccessRevoked";
        var messageFa = normalizedAction == "grant"
            ? $"دسترسی همکاری به تکنسین {targetName} داده شد"
            : $"دسترسی همکاری از تکنسین {targetName} لغو شد";
        var metadataJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            messageFa,
            targetTechnicianUserId = technicianUserId,
            targetTechnicianName = targetName
        });
        await _activityEventRepository.AddEventAsync(ticketId, actorUserId, actorRoleLabel, eventType, null, null, metadataJson);

        // Reload ticket for accurate response + broadcast
        ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null) return null;

        try
        {
            if (_ticketHubService != null)
            {
                // Notify all interested users that access changed (UI should refresh)
                var userIds = new HashSet<Guid> { ticket.CreatedByUserId, technicianUserId };
                if (ticket.AssignedToUserId.HasValue) userIds.Add(ticket.AssignedToUserId.Value);
                foreach (var a in ticket.AssignedTechnicians.Where(a => a.IsActive))
                {
                    userIds.Add(a.TechnicianUserId);
                }
                var supervisorIds = await GetSupervisorsForTicketAsync(ticket);
                foreach (var sup in supervisorIds) userIds.Add(sup);
                var admins = await _userRepository.GetByRoleAsync(UserRole.Admin.ToString());
                foreach (var admin in admins) userIds.Add(admin.Id);

                await _ticketHubService.BroadcastTicketUpdateAsync(
                    ticketId,
                    "TicketAccessChanged",
                    new { technicianUserId, action = normalizedAction },
                    userIds);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "UpdateCollaboratorAsync: Failed to broadcast access change for TicketId={TicketId}", ticketId);
        }

        return await GetTicketAsync(ticketId, actorUserId, actorRole);
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // MANUAL TEST CHECKLIST (Swagger):
    // 1. POST /api/Tickets → status=Submitted, assignedToUserId=null, assignedToName/email/phone=null
    // 2. POST /api/admin/assignment/smart/run → assignedCount > 0 (if eligible unassigned tickets exist)
    // 3. GET /api/technician/tickets (as assigned tech) → ticket appears in list
    // ═══════════════════════════════════════════════════════════════════════════════
    private async Task<bool> CanSupervisorAccessTicketAsync(Guid supervisorUserId, Ticket ticket)
    {
        var supervisor = await _technicianRepository.GetByUserIdAsync(supervisorUserId);
        if (supervisor == null || !supervisor.IsSupervisor)
        {
            return false;
        }

        var linkedTechnicians = await _supervisorLinkRepository.GetLinksForSupervisorAsync(supervisorUserId);
        var linkedIds = linkedTechnicians.Select(l => l.TechnicianUserId).ToHashSet();
        return ticket.AssignedTechnicians?.Any(ta => ta.IsActive && linkedIds.Contains(ta.TechnicianUserId)) ?? false;
    }

    private async Task<string> GetActorRoleLabelAsync(User? actor, UserRole fallbackRole)
    {
        if (fallbackRole == UserRole.Admin)
        {
            return "Admin";
        }

        if (fallbackRole == UserRole.Client)
        {
            return "Client";
        }

        if (actor == null)
        {
            return "Technician";
        }

        var technician = await _technicianRepository.GetByUserIdAsync(actor.Id);
        return technician != null && technician.IsSupervisor ? "Supervisor" : "Technician";
    }

    /// <summary>
    /// Returns display role for timeline: Owner, Collaborator, Admin, Supervisor, or Client.
    /// Use when logging reply/status/attachment so the timeline shows who did it in what capacity.
    /// </summary>
    private async Task<string> GetActorRoleLabelForTicketAsync(Ticket ticket, Guid userId, UserRole role)
    {
        if (role == UserRole.Admin) return "Admin";
        if (role == UserRole.Client) return "Client";
        if (role != UserRole.Technician) return "Technician";
        if (ticket.AssignedToUserId == userId) return "Owner";
        var assignment = ticket.AssignedTechnicians?.FirstOrDefault(ta => ta.TechnicianUserId == userId && ta.IsActive);
        if (assignment != null && string.Equals(assignment.Role, "Collaborator", StringComparison.OrdinalIgnoreCase))
            return "Collaborator";
        var technician = await _technicianRepository.GetByUserIdAsync(userId);
        return technician != null && technician.IsSupervisor ? "Supervisor" : "Technician";
    }

    private async Task<HashSet<Guid>> GetSupervisorsForTicketAsync(Ticket ticket)
    {
        var supervisorIds = new HashSet<Guid>();
        var assignedTechIds = ticket.AssignedTechnicians?
            .Where(ta => ta.IsActive)
            .Select(ta => ta.TechnicianUserId)
            .Distinct()
            .ToList() ?? new List<Guid>();

        foreach (var technicianUserId in assignedTechIds)
        {
            var links = await _supervisorLinkRepository.GetLinksForTechnicianAsync(technicianUserId);
            foreach (var link in links)
            {
                supervisorIds.Add(link.SupervisorUserId);
            }
        }

        return supervisorIds;
    }

    /// <summary>
    /// Maps a Ticket entity to a TicketResponse DTO with role-based status mapping.
    /// Uses StatusMappingService to determine displayStatus based on requester's role.
    /// </summary>
    /// <param name="ticket">The ticket entity to map</param>
    /// <param name="userId">The user ID of the requester (for unseen calculation)</param>
    /// <param name="lastSeenAt">When the user last saw this ticket</param>
    /// <param name="requesterRole">The role of the requester (for status mapping)</param>
    private TicketResponse MapToResponse(Ticket ticket, Guid? userId = null, DateTime? lastSeenAt = null, UserRole? requesterRole = null)
    {
        try
        {
            // SECURITY-CRITICAL: Only show assigned technician info when ticket is truly assigned
            // "Truly assigned" = AssignedToUserId is not null (the authoritative field for filtering/queries)
            var isAssigned = ticket.AssignedToUserId != null;
            
            // Map canonical status to display status based on requester's role
            var canonicalStatus = ticket.Status;
            var displayStatus = requesterRole.HasValue 
                ? StatusMappingService.MapStatusForRole(canonicalStatus, requesterRole.Value)
                : canonicalStatus; // Default to canonical if role not specified
            
            // Map field values (safe null handling)
            var dynamicFields = ticket.FieldValues?
                .Where(fv => fv != null)
                .Select(fv => new DTOs.TicketDynamicFieldResponse
                {
                    FieldDefinitionId = fv.FieldDefinitionId,
                    Key = fv.FieldDefinition?.FieldKey ?? string.Empty,
                    Label = fv.FieldDefinition?.Label ?? string.Empty,
                    Type = fv.FieldDefinition?.Type.ToString() ?? string.Empty,
                    Value = fv.Value ?? string.Empty,
                    IsRequired = fv.FieldDefinition?.IsRequired ?? false
                })
                .ToList() ?? new List<DTOs.TicketDynamicFieldResponse>();
        
        var activityEvents = ticket.ActivityEvents?
            .Select(ae => new DTOs.TicketActivityEventDto
            {
                Id = ae.Id,
                TicketId = ae.TicketId,
                ActorUserId = ae.ActorUserId,
                ActorName = ae.ActorUser?.FullName ?? "Unknown",
                ActorRole = ae.ActorRole,
                EventType = ae.EventType,
                OldStatus = ae.OldStatus,
                NewStatus = ae.NewStatus,
                MetadataJson = ae.MetadataJson,
                CreatedAt = ae.CreatedAt
            })
            .OrderByDescending(ae => ae.CreatedAt)
            .ToList();

        var latestEvent = activityEvents?.FirstOrDefault();
        var lastActivityAt = latestEvent?.CreatedAt ?? ticket.UpdatedAt ?? ticket.CreatedAt;
        var isUnseen = userId.HasValue
            ? !lastSeenAt.HasValue || lastActivityAt > lastSeenAt.Value
            : (bool?)null;

        var latestActivitySummary = latestEvent == null
            ? null
            : latestEvent.EventType == "StatusChanged" || latestEvent.EventType == "Revision"
                ? $"Status changed: {latestEvent.OldStatus ?? "-"} -> {latestEvent.NewStatus ?? "-"}"
                : latestEvent.EventType == "ReplyAdded"
                    ? "Reply added"
                    : latestEvent.EventType == "AssignedTechnicians"
                        ? "Technicians assigned"
                        : latestEvent.EventType == "Handoff"
                            ? "Ticket reassigned"
                            : latestEvent.EventType == "Created"
                                ? "Ticket created"
                                : latestEvent.EventType;

        // Acceptance: only true when at least one assignment has AcceptedAt (do not treat Assigned as Accepted)
        var firstAccepted = ticket.AssignedTechnicians?
            .Where(a => a.AcceptedAt != null)
            .OrderBy(a => a.AcceptedAt)
            .FirstOrDefault();

        return new TicketResponse
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            CategoryId = ticket.CategoryId,
            CategoryName = ticket.Category?.Name ?? string.Empty,
            SubcategoryId = ticket.SubcategoryId,
            SubcategoryName = ticket.Subcategory?.Name,
            Priority = ticket.Priority,
            // Set both canonical and display status
            CanonicalStatus = canonicalStatus,
            DisplayStatus = displayStatus,
            CreatedByUserId = ticket.CreatedByUserId,
            CreatedByName = ticket.CreatedByUser?.FullName ?? string.Empty,
            CreatedByEmail = ticket.CreatedByUser?.Email ?? string.Empty,
            CreatedByPhoneNumber = ticket.CreatedByUser?.PhoneNumber,
            CreatedByDepartment = ticket.CreatedByUser?.Department,
            AssignedToUserId = ticket.AssignedToUserId,
            // Only populate assigned fields when truly assigned
            AssignedToName = isAssigned ? (ticket.Technician?.FullName ?? ticket.AssignedToUser?.FullName) : null,
            AssignedToEmail = isAssigned ? (ticket.Technician?.Email ?? ticket.AssignedToUser?.Email) : null,
            AssignedToPhoneNumber = isAssigned ? (ticket.Technician?.Phone ?? ticket.AssignedToUser?.PhoneNumber) : null,
            AssignedTechnicianName = isAssigned ? (ticket.Technician?.FullName ?? ticket.AssignedToUser?.FullName) : null,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            DueDate = ticket.DueDate,
            DynamicFields = dynamicFields,
            AssignedTechnicians = BuildAssignedTechniciansForResponse(ticket),
            ActivityEvents = activityEvents,
            LastActivityAt = lastActivityAt,
            LastSeenAt = lastSeenAt,
            IsUnseen = isUnseen,
            IsUnread = isUnseen,
            LatestActivity = latestEvent == null
                ? null
                : new DTOs.TicketLatestActivityDto
                {
                    ActionType = latestEvent.EventType,
                    ActorName = latestEvent.ActorName,
                    ActorRole = latestEvent.ActorRole,
                    CreatedAt = latestEvent.CreatedAt,
                    FromStatus = latestEvent.OldStatus,
                    ToStatus = latestEvent.NewStatus,
                    Summary = latestActivitySummary
                },
            IsAccepted = firstAccepted != null,
            AcceptedAt = firstAccepted?.AcceptedAt,
            AcceptedByUserId = firstAccepted?.TechnicianUserId
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MapToResponse FAILED for ticket {TicketId}. Exception: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                ticket?.Id, ex.GetType().Name, ex.Message, ex.StackTrace);
            
            // Return minimal response to avoid breaking the entire list
            var fallbackCanonical = ticket?.Status ?? Domain.Enums.TicketStatus.Submitted;
            var fallbackDisplay = requesterRole.HasValue 
                ? StatusMappingService.MapStatusForRole(fallbackCanonical, requesterRole.Value)
                : fallbackCanonical;
                
            return new TicketResponse
            {
                Id = ticket?.Id ?? Guid.Empty,
                Title = ticket?.Title ?? "Error loading ticket",
                Description = ticket?.Description ?? "",
                CategoryId = ticket?.CategoryId ?? 0,
                CategoryName = ticket?.Category?.Name ?? "Unknown",
                SubcategoryId = ticket?.SubcategoryId,
                Priority = ticket?.Priority ?? Domain.Enums.TicketPriority.Medium,
                CanonicalStatus = fallbackCanonical,
                DisplayStatus = fallbackDisplay,
                CreatedByUserId = ticket?.CreatedByUserId ?? Guid.Empty,
                CreatedByName = ticket?.CreatedByUser?.FullName ?? "Unknown",
                CreatedByEmail = ticket?.CreatedByUser?.Email ?? "",
                CreatedAt = ticket?.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = ticket?.UpdatedAt,
                LastActivityAt = ticket?.UpdatedAt ?? ticket?.CreatedAt,
                DynamicFields = new List<DTOs.TicketDynamicFieldResponse>(),
                AssignedTechnicians = new List<DTOs.AssignedTechnicianDto>(),
                ActivityEvents = new List<DTOs.TicketActivityEventDto>(),
                IsAccepted = false,
                AcceptedAt = null,
                AcceptedByUserId = null
            };
        }
    }

    /// <summary>
    /// Builds assigned technicians list for GET ticket: includes owner (from AssignedToUserId) as first entry
    /// and all TicketTechnicianAssignments with AccessMode/CanAct for UI "تکنسین‌های واگذار شده (X فعال)".
    /// </summary>
    private List<DTOs.AssignedTechnicianDto> BuildAssignedTechniciansForResponse(Ticket ticket)
    {
        var list = new List<DTOs.AssignedTechnicianDto>();
        var assignments = ticket.AssignedTechnicians?.ToList() ?? new List<TicketTechnicianAssignment>();
        var isClaimed = ticket.AssignedToUserId != null;

        // 1) Owner first: from AssignedToUserId (synthetic if not in assignments)
        if (ticket.AssignedToUserId.HasValue)
        {
            var ownerId = ticket.AssignedToUserId.Value;
            var ownerAssignment = assignments.FirstOrDefault(ta => ta.TechnicianUserId == ownerId);
            var ownerName = ticket.Technician?.FullName ?? ticket.AssignedToUser?.FullName ?? "—";
            var ownerEmail = ticket.Technician?.Email ?? ticket.AssignedToUser?.Email;
            var ownerAssignedAt = ownerAssignment?.AssignedAt ?? ticket.UpdatedAt ?? ticket.CreatedAt;
            list.Add(new DTOs.AssignedTechnicianDto
            {
                Id = ownerAssignment?.Id ?? Guid.Empty,
                TechnicianUserId = ownerId,
                TechnicianName = ownerName,
                TechnicianEmail = ownerEmail,
                IsActive = true,
                AssignedAt = ownerAssignedAt,
                Role = "Owner",
                AccessMode = "Owner",
                CanAct = true
            });
        }

        // 2) All assignments: skip owner (already added), add Collaborators and Candidates with AccessMode/CanAct
        foreach (var ta in assignments.Where(ta => ta.TechnicianUserId != ticket.AssignedToUserId))
        {
            var isOwner = ta.TechnicianUserId == ticket.AssignedToUserId;
            var isCollaborator = string.Equals(ta.Role, "Collaborator", StringComparison.OrdinalIgnoreCase) && ta.IsActive;
            var isCandidate = string.Equals(ta.Role, "Candidate", StringComparison.OrdinalIgnoreCase) || string.Equals(ta.Role, "Lead", StringComparison.OrdinalIgnoreCase);
            // Lead on assignment that is not the ticket owner = was from old claim; treat as Candidate for display if not owner
            var accessMode = isOwner ? "Owner" : isCollaborator ? "Collaborator" : "Candidate";
            var canAct = isOwner || isCollaborator;
            var isActiveForUi = canAct && ta.IsActive;
            if (isCandidate && isClaimed)
                isActiveForUi = false;

            list.Add(new DTOs.AssignedTechnicianDto
            {
                Id = ta.Id,
                TechnicianUserId = ta.TechnicianUserId,
                TechnicianName = ta.TechnicianUser?.FullName ?? string.Empty,
                TechnicianEmail = ta.TechnicianUser?.Email,
                IsActive = isActiveForUi,
                AssignedAt = ta.AssignedAt,
                Role = ta.Role,
                AccessMode = accessMode,
                CanAct = canAct
            });
        }

        return list;
    }

    public async Task<IEnumerable<TicketCalendarResponse>> GetCalendarTicketsAsync(DateTime startDate, DateTime endDate)
    {
        // Get all tickets within the date range (Admin only - no role filtering)
        var tickets = await _ticketRepository.GetCalendarTicketsAsync(startDate, endDate);

        return tickets.Select(t => new TicketCalendarResponse
        {
            Id = t.Id,
            TicketNumber = $"T-{t.Id.ToString("N").Substring(0, 8).ToUpper()}",
            Title = t.Title,
            // Admin view - show canonical status as-is (admin can see Redo)
            CanonicalStatus = t.Status,
            DisplayStatus = StatusMappingService.MapStatusForRole(t.Status, UserRole.Admin),
            Priority = t.Priority,
            CategoryName = t.Category?.Name ?? string.Empty,
            // Only show technician name when truly assigned (AssignedToUserId != null)
            AssignedTechnicianName = t.AssignedToUserId != null ? (t.Technician?.FullName ?? t.AssignedToUser?.FullName) : null,
            CreatedAt = t.CreatedAt,
            DueDate = t.DueDate
        });
    }

    // Multi-technician assignment methods
    public async Task<TicketResponse?> AssignTechniciansAsync(Guid ticketId, List<Guid> technicianUserIds, Guid assignedByUserId, Guid? leadTechnicianUserId = null)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return null;
        }

        // Authorization: Check if user is Admin or Supervisor assigned to ticket
        var assignedByUser = await _userRepository.GetByIdAsync(assignedByUserId);
        if (assignedByUser == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Admin can always assign
        if (assignedByUser.Role != UserRole.Admin)
        {
            // For Technician role, check if they are a supervisor assigned to this ticket
            if (assignedByUser.Role == UserRole.Technician)
            {
                // Check if technician is assigned to this ticket
                var isAssignedToTicket = ticket.AssignedTechnicians?
                    .Any(ta => ta.TechnicianUserId == assignedByUserId && ta.IsActive) ?? false;
                
                if (!isAssignedToTicket)
                {
                    throw new UnauthorizedAccessException("Only assigned supervisors can reassign tickets");
                }

                // Check if technician is a supervisor
                var technician = await _technicianRepository.GetByUserIdAsync(assignedByUserId);
                if (technician == null || !technician.IsSupervisor)
                {
                    throw new UnauthorizedAccessException("Only supervisors can assign/reassign tickets");
                }
            }
            else
            {
                throw new UnauthorizedAccessException("Only Admin or assigned Supervisor technicians can assign tickets");
            }
        }

        // Validate technicians exist
        var technicians = new List<User>();
        foreach (var techUserId in technicianUserIds)
        {
            var tech = await _userRepository.GetByIdAsync(techUserId);
            if (tech == null || tech.Role != UserRole.Technician)
            {
                throw new ArgumentException($"Technician user {techUserId} not found or is not a technician");
            }
            technicians.Add(tech);
        }

        // Set assignments
        await _assignmentRepository.SetAssignmentsAsync(
            ticketId,
            technicianUserIds,
            assignedByUserId,
            leadTechnicianUserId?.ToString());

        // Update ticket status to Open if it was Submitted
        var oldStatus = ticket.Status.ToString();
        if (ticket.Status == TicketStatus.Submitted)
        {
            ticket.Status = TicketStatus.Open;
            ticket.UpdatedAt = DateTime.UtcNow;
            await _ticketRepository.UpdateAsync(ticket);
        }

        await _unitOfWork.SaveChangesAsync();

        // Create activity event
        var assignedBy = await _userRepository.GetByIdAsync(assignedByUserId);
        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            technicianUserIds = technicianUserIds,
            leadTechnicianUserId = leadTechnicianUserId
        });
        await _activityEventRepository.AddEventAsync(
            ticketId,
            assignedByUserId,
            assignedBy?.Role == UserRole.Admin ? "Admin" : "Technician",
            "AssignedTechnicians",
            oldStatus,
            ticket.Status.ToString(),
            metadata);

        // Reload ticket with includes
        ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null) return null;

        // PHASE 3: Broadcast assignment change via SignalR
        await BroadcastAssignmentChangedAsync(ticket, technicianUserIds, assignedByUserId);

        return await GetTicketAsync(ticketId, assignedByUserId, assignedByUser.Role);
    }

    public async Task<TicketResponse?> HandoffTicketAsync(Guid ticketId, Guid fromTechnicianUserId, Guid toTechnicianUserId, bool deactivateCurrent = true, UserRole? requesterRole = null)
    {
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return null;
        }

        var isAdmin = requesterRole == UserRole.Admin;
        // When Admin performs handoff, we deactivate the current owner's assignment (whoever AssignedToUserId is)
        var fromUserId = isAdmin && ticket.AssignedToUserId.HasValue ? ticket.AssignedToUserId.Value : fromTechnicianUserId;
        var fromAssignment = ticket.AssignedTechnicians?.FirstOrDefault(ta => ta.TechnicianUserId == fromUserId && ta.IsActive);
        var isOwner = ticket.AssignedToUserId == fromTechnicianUserId;
        if (!isAdmin)
        {
            if (fromAssignment == null && !isOwner)
            {
                throw new UnauthorizedAccessException("You are not assigned to this ticket");
            }
            var fromTechnician = await _technicianRepository.GetByUserIdAsync(fromTechnicianUserId);
            if (fromTechnician == null || !fromTechnician.IsSupervisor)
            {
                throw new UnauthorizedAccessException("Only supervisors can reassign tickets");
            }
        }

        // Verify to technician exists and is a technician
        var toTechnician = await _userRepository.GetByIdAsync(toTechnicianUserId);
        if (toTechnician == null || toTechnician.Role != UserRole.Technician)
        {
            throw new ArgumentException("Target technician not found or is not a technician");
        }

        // Deactivate current assignment if requested (owner may have no assignment row)
        if (deactivateCurrent && fromAssignment != null)
        {
            fromAssignment.IsActive = false;
            fromAssignment.UpdatedAt = DateTime.UtcNow;
            await _assignmentRepository.UpdateAsync(fromAssignment);
        }

        // Add or reactivate new assignment
        var existingToAssignment = ticket.AssignedTechnicians?.FirstOrDefault(ta => ta.TechnicianUserId == toTechnicianUserId);
        if (existingToAssignment != null)
        {
            existingToAssignment.IsActive = true;
            existingToAssignment.AssignedAt = DateTime.UtcNow;
            existingToAssignment.AssignedByUserId = fromTechnicianUserId;
            existingToAssignment.UpdatedAt = DateTime.UtcNow;
            existingToAssignment.Role = "Owner";
            await _assignmentRepository.UpdateAsync(existingToAssignment);
        }
        else
        {
            var newAssignment = new Domain.Entities.TicketTechnicianAssignment
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                TechnicianUserId = toTechnicianUserId,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = fromTechnicianUserId,
                IsActive = true,
                Role = "Owner"
            };
            await _assignmentRepository.AddAsync(newAssignment);
        }

        // New owner: set AssignedToUserId and TechnicianId so the ticket is owned by the target technician
        var toTechnicianEntity = await _technicianRepository.GetByUserIdAsync(toTechnicianUserId);
        if (toTechnicianEntity != null)
        {
            ticket.AssignedToUserId = toTechnicianUserId;
            ticket.TechnicianId = toTechnicianEntity.Id;
        }
        ticket.UpdatedAt = DateTime.UtcNow;
        await _ticketRepository.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        // Create activity event
        var metadata = System.Text.Json.JsonSerializer.Serialize(new
        {
            fromTechnicianUserId = fromTechnicianUserId,
            toTechnicianUserId = toTechnicianUserId,
            deactivateCurrent = deactivateCurrent
        });
        await _activityEventRepository.AddEventAsync(
            ticketId,
            fromTechnicianUserId,
            "Technician",
            "Handoff",
            null,
            null,
            metadata);

        // Reload ticket with includes
        ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null) return null;

        // PHASE 3: Broadcast handoff via SignalR
        await BroadcastAssignmentChangedAsync(ticket, new List<Guid> { toTechnicianUserId }, fromTechnicianUserId);

        // Handoff is done by technicians (supervisors)
        return await GetTicketAsync(ticketId, fromTechnicianUserId, UserRole.Technician);
    }

    public async Task<List<Ticketing.Backend.Application.DTOs.TicketActivityDto>> GetTicketActivitiesAsync(Guid ticketId, Guid userId, UserRole role)
    {
        // Verify ticket exists and user has access
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            return new List<Ticketing.Backend.Application.DTOs.TicketActivityDto>();
        }

        // Check access: Admin, ticket owner (client), or eligible technician
        var hasAccess = role == UserRole.Admin || ticket.CreatedByUserId == userId;
        if (!hasAccess && role == UserRole.Technician)
        {
            hasAccess = await CanTechnicianViewTicketAsync(userId, ticket);
        }

        if (!hasAccess)
        {
            return new List<Ticketing.Backend.Application.DTOs.TicketActivityDto>();
        }

        // Get activity events for this ticket
        var events = await _activityEventRepository.GetEventsForTicketAsync(ticketId);
        
        // Map to TicketActivityDto (from Ticketing.Backend.Application.DTOs namespace)
        // Map EventType string to TicketActivityType enum
        return events.Select(ae => {
            // Map common event types to enum values
            Ticketing.Backend.Domain.Enums.TicketActivityType activityType = Ticketing.Backend.Domain.Enums.TicketActivityType.Updated;
            if (!string.IsNullOrEmpty(ae.EventType))
            {
                // Try direct parse first
                if (Enum.TryParse<Ticketing.Backend.Domain.Enums.TicketActivityType>(ae.EventType, true, out var parsed))
                {
                    activityType = parsed;
                }
                else
                {
                    // Map common string values to enum
                    activityType = ae.EventType switch
                    {
                        "StatusChanged" => Ticketing.Backend.Domain.Enums.TicketActivityType.StatusChanged,
                        "Assigned" => Ticketing.Backend.Domain.Enums.TicketActivityType.Assigned,
                        "ReplyAdded" or "MessageAdded" => Ticketing.Backend.Domain.Enums.TicketActivityType.MessageAdded,
                        "Handoff" => Ticketing.Backend.Domain.Enums.TicketActivityType.AssignmentChanged,
                        "CommentAdded" => Ticketing.Backend.Domain.Enums.TicketActivityType.CommentAdded,
                        "Created" => Ticketing.Backend.Domain.Enums.TicketActivityType.Created,
                        "Closed" => Ticketing.Backend.Domain.Enums.TicketActivityType.Closed,
                        "Reopened" => Ticketing.Backend.Domain.Enums.TicketActivityType.Reopened,
                        "AccessGranted" => Ticketing.Backend.Domain.Enums.TicketActivityType.AccessGranted,
                        "AccessRevoked" => Ticketing.Backend.Domain.Enums.TicketActivityType.AccessRevoked,
                        _ => Ticketing.Backend.Domain.Enums.TicketActivityType.Updated
                    };
                }
            }
            
            return new Ticketing.Backend.Application.DTOs.TicketActivityDto
            {
                Id = ae.Id,
                TicketId = ae.TicketId,
                ActorUserId = ae.ActorUserId,
                ActorName = ae.ActorUser?.FullName ?? "Unknown",
                ActorEmail = ae.ActorUser?.Email ?? "",
                Type = activityType,
                Message = ae.MetadataJson ?? "",
                CreatedAt = ae.CreatedAt
            };
        }).OrderByDescending(ae => ae.CreatedAt).ToList();
    }

    /// <summary>
    /// CENTRALIZED STATUS CHANGE METHOD - Single Source of Truth
    /// All status changes MUST go through this method to ensure:
    /// 1. Consistent authorization validation
    /// 2. Single canonical status field update (Ticket.Status)
    /// 3. Activity event logging
    /// 4. Real-time update broadcast
    /// 5. Multi-assignee visibility (all technicians see same status)
    /// </summary>
    public async Task<StatusChangeResult> ChangeStatusAsync(Guid ticketId, TicketStatus newStatus, Guid actorUserId, UserRole actorRole)
    {
        _logger?.LogInformation("ChangeStatusAsync: TicketId={TicketId}, NewStatus={NewStatus}, ActorUserId={ActorUserId}, ActorRole={ActorRole}",
            ticketId, newStatus, actorUserId, actorRole);

        // 1. Load ticket with all includes for proper authorization check
        var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
        if (ticket == null)
        {
            _logger?.LogWarning("ChangeStatusAsync: Ticket {TicketId} not found", ticketId);
            return new StatusChangeResult 
            { 
                Success = false, 
                ErrorMessage = "Ticket not found" 
            };
        }

        var oldStatus = ticket.Status;

        // 2. Authorization validation
        // Enforce centralized access policy first (read-only technicians must not mutate)
        var access = await GetTicketAccessForLoadedTicketAsync(ticket, actorUserId, actorRole);
        if (!access.CanEdit)
        {
            _logger?.LogWarning("ChangeStatusAsync: Forbidden by access policy. TicketId={TicketId}, ActorUserId={ActorUserId}, Role={Role}, Reason={Reason}",
                ticketId, actorUserId, actorRole, access.ReadOnlyReason);
            return new StatusChangeResult
            {
                Success = false,
                ErrorMessage = "Ticket is read-only for you.",
                OldStatus = oldStatus
            };
        }

        var authResult = await ValidateStatusChangeAuthorizationAsync(ticket, newStatus, actorUserId, actorRole);
        if (!authResult.IsAuthorized)
        {
            _logger?.LogWarning("ChangeStatusAsync: Unauthorized. TicketId={TicketId}, ActorUserId={ActorUserId}, ActorRole={ActorRole}, Reason={Reason}",
                ticketId, actorUserId, actorRole, authResult.Reason);
            return new StatusChangeResult 
            { 
                Success = false, 
                ErrorMessage = authResult.Reason,
                OldStatus = oldStatus 
            };
        }

        // 3. If status is same, no-op (return success but don't log activity)
        if (oldStatus == newStatus)
        {
            _logger?.LogInformation("ChangeStatusAsync: Status unchanged (already {Status})", newStatus);
            var unchangedResponse = await GetTicketAsync(ticketId, actorUserId, actorRole);
            return new StatusChangeResult 
            { 
                Success = true, 
                OldStatus = oldStatus, 
                NewStatus = newStatus,
                Ticket = unchangedResponse
            };
        }

        // 4. Update the CANONICAL status field (single source of truth)
        ticket.Status = newStatus;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _ticketRepository.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        _logger?.LogInformation("ChangeStatusAsync: Status changed. TicketId={TicketId}, OldStatus={OldStatus}, NewStatus={NewStatus}",
            ticketId, oldStatus, newStatus);

        // 5. Create activity event for audit trail (Owner/Collaborator/Admin/Supervisor for timeline)
        var actorRoleLabel = await GetActorRoleLabelForTicketAsync(ticket, actorUserId, actorRole);
        var eventType = newStatus == TicketStatus.Redo ? "Revision" : "StatusChanged";
        
        await _activityEventRepository.AddEventAsync(
            ticketId,
            actorUserId,
            actorRoleLabel,
            eventType,
            oldStatus.ToString(),
            newStatus.ToString(),
            null);

        // 6. Broadcast via SignalR for real-time updates (if available)
        // Note: This will be implemented in Phase 3 with TicketHub
        await BroadcastTicketUpdateAsync(ticketId, newStatus, oldStatus);

        // 7. Return updated ticket
        var updatedTicket = await GetTicketAsync(ticketId, actorUserId, actorRole);
        return new StatusChangeResult 
        { 
            Success = true, 
            OldStatus = oldStatus, 
            NewStatus = newStatus,
            Ticket = updatedTicket
        };
    }

    /// <summary>
    /// Validates if the actor is authorized to change the ticket status
    /// </summary>
    private async Task<(bool IsAuthorized, string? Reason)> ValidateStatusChangeAuthorizationAsync(
        Ticket ticket, TicketStatus newStatus, Guid actorUserId, UserRole actorRole)
    {
        // Admin can change any status
        if (actorRole == UserRole.Admin)
        {
            return (true, null);
        }

        // Client restrictions
        if (actorRole == UserRole.Client)
        {
            // Client can only change their own tickets
            if (ticket.CreatedByUserId != actorUserId)
            {
                return (false, "Clients can only modify their own tickets");
            }

            // Client cannot set InProgress, Solved, or Redo
            if (newStatus == TicketStatus.InProgress || 
                newStatus == TicketStatus.Solved || 
                newStatus == TicketStatus.Redo)
            {
                return (false, "Clients cannot set status to InProgress, Solved, or Redo");
            }

            return (true, null);
        }

        // Technician (including Supervisor) restrictions
        if (actorRole == UserRole.Technician)
        {
            // Check if technician is assigned to this ticket
            var isAssignedOld = ticket.TechnicianId == actorUserId || ticket.AssignedToUserId == actorUserId;
            var isAssignedNew = ticket.AssignedTechnicians?.Any(ta => ta.TechnicianUserId == actorUserId && ta.IsActive) ?? false;
            
            if (isAssignedOld || isAssignedNew)
            {
                return (true, null);
            }

            // Check if supervisor has access through their team
            var isSupervisorScope = await CanSupervisorAccessTicketAsync(actorUserId, ticket);
            if (isSupervisorScope)
            {
                return (true, null);
            }

            return (false, "Technicians can only modify tickets assigned to them");
        }

        return (false, "Unknown role");
    }

    /// <summary>
    /// Broadcasts ticket update via SignalR to all connected clients
    /// This enables real-time synchronization across all dashboards
    /// </summary>
    /// <summary>
    /// Broadcasts an assignment change event via SignalR for real-time synchronization.
    /// This ensures all dashboards see assignment changes immediately.
    /// </summary>
    private async Task BroadcastAssignmentChangedAsync(Ticket ticket, List<Guid> newTechnicianUserIds, Guid assignedByUserId)
    {
        try
        {
            // Collect all user IDs who should receive the update
            var userIds = new HashSet<Guid>();
            
            // Add ticket creator (client)
            userIds.Add(ticket.CreatedByUserId);
            
            // Add all assigned technicians (including newly assigned)
            foreach (var techId in newTechnicianUserIds)
            {
                userIds.Add(techId);
            }
            if (ticket.AssignedToUserId.HasValue)
            {
                userIds.Add(ticket.AssignedToUserId.Value);
            }
            foreach (var assignment in ticket.AssignedTechnicians?.Where(ta => ta.IsActive) ?? Enumerable.Empty<TicketTechnicianAssignment>())
            {
                userIds.Add(assignment.TechnicianUserId);
            }

            // Add supervisors for assigned technicians
            var supervisorIds = await GetSupervisorsForTicketAsync(ticket);
            foreach (var supervisorId in supervisorIds)
            {
                userIds.Add(supervisorId);
            }

            // Add all admins
            var admins = await _userRepository.GetByRoleAsync(UserRole.Admin.ToString());
            foreach (var admin in admins)
            {
                userIds.Add(admin.Id);
            }

            _logger?.LogInformation(
                "BroadcastAssignmentChangedAsync: TicketId={TicketId}, NewTechnicianCount={Count}, broadcasting to {UserCount} users",
                ticket.Id, newTechnicianUserIds.Count, userIds.Count);

            // Use SignalR for real-time broadcast if available
            if (_ticketHubService != null)
            {
                await _ticketHubService.BroadcastTicketUpdateAsync(
                    ticket.Id,
                    "AssignmentChanged",
                    new 
                    { 
                        newTechnicianUserIds = newTechnicianUserIds,
                        assignedByUserId = assignedByUserId
                    },
                    userIds);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the assignment if broadcast fails
            _logger?.LogWarning(ex, "BroadcastAssignmentChangedAsync: Failed to broadcast update for TicketId={TicketId}", ticket.Id);
        }
    }

    /// <summary>
    /// Broadcasts a reply added event via SignalR for real-time synchronization.
    /// This ensures all dashboards see new replies immediately.
    /// </summary>
    private async Task BroadcastReplyAddedAsync(Ticket ticket, TicketMessage message, User author)
    {
        try
        {
            // Collect all user IDs who should receive the update
            var userIds = new HashSet<Guid>();
            
            // Add ticket creator (client)
            userIds.Add(ticket.CreatedByUserId);
            
            // Add all assigned technicians
            if (ticket.AssignedToUserId.HasValue)
            {
                userIds.Add(ticket.AssignedToUserId.Value);
            }
            foreach (var assignment in ticket.AssignedTechnicians?.Where(ta => ta.IsActive) ?? Enumerable.Empty<TicketTechnicianAssignment>())
            {
                userIds.Add(assignment.TechnicianUserId);
            }

            // Add supervisors for assigned technicians
            var supervisorIds = await GetSupervisorsForTicketAsync(ticket);
            foreach (var supervisorId in supervisorIds)
            {
                userIds.Add(supervisorId);
            }

            // Add all admins
            var admins = await _userRepository.GetByRoleAsync(UserRole.Admin.ToString());
            foreach (var admin in admins)
            {
                userIds.Add(admin.Id);
            }

            _logger?.LogInformation(
                "BroadcastReplyAddedAsync: TicketId={TicketId}, AuthorUserId={AuthorUserId}, broadcasting to {UserCount} users",
                ticket.Id, author.Id, userIds.Count);

            // Use SignalR for real-time broadcast if available
            if (_ticketHubService != null)
            {
                var actorRole = await GetActorRoleLabelAsync(author, author.Role);
                await _ticketHubService.BroadcastTicketUpdateAsync(
                    ticket.Id,
                    "ReplyAdded",
                    new 
                    { 
                        messageId = message.Id,
                        authorName = author.FullName,
                        authorRole = actorRole,
                        messagePreview = message.Message?.Substring(0, Math.Min(100, message.Message?.Length ?? 0)),
                        createdAt = message.CreatedAt
                    },
                    userIds);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the message creation if broadcast fails
            _logger?.LogWarning(ex, "BroadcastReplyAddedAsync: Failed to broadcast update for TicketId={TicketId}", ticket.Id);
        }
    }

    /// <summary>
    /// Broadcasts ticket status update via SignalR for real-time synchronization.
    /// This ensures all dashboards (Client/Technician/Supervisor/Admin) see the same status.
    /// </summary>
    private async Task BroadcastTicketUpdateAsync(Guid ticketId, TicketStatus newStatus, TicketStatus oldStatus)
    {
        try
        {
            // Load ticket to get all participants who should receive the update
            var ticket = await _ticketRepository.GetByIdWithIncludesAsync(ticketId);
            if (ticket == null) return;

            // Collect all user IDs who should receive the update
            var userIds = new HashSet<Guid>();
            
            // Add ticket creator (client)
            userIds.Add(ticket.CreatedByUserId);
            
            // Add all assigned technicians (both old and new assignment system)
            if (ticket.AssignedToUserId.HasValue)
            {
                userIds.Add(ticket.AssignedToUserId.Value);
            }
            foreach (var assignment in ticket.AssignedTechnicians?.Where(ta => ta.IsActive) ?? Enumerable.Empty<TicketTechnicianAssignment>())
            {
                userIds.Add(assignment.TechnicianUserId);
            }

            // Add supervisors for assigned technicians
            var supervisorIds = await GetSupervisorsForTicketAsync(ticket);
            foreach (var supervisorId in supervisorIds)
            {
                userIds.Add(supervisorId);
            }

            // Add all admins (they see all tickets)
            var admins = await _userRepository.GetByRoleAsync(UserRole.Admin.ToString());
            foreach (var admin in admins)
            {
                userIds.Add(admin.Id);
            }

            _logger?.LogInformation(
                "BroadcastTicketUpdateAsync: TicketId={TicketId}, {OldStatus} → {NewStatus}, broadcasting to {UserCount} users",
                ticketId, oldStatus, newStatus, userIds.Count);

            // Use SignalR for real-time broadcast if available
            if (_ticketHubService != null)
            {
                await _ticketHubService.BroadcastStatusUpdateAsync(
                    ticketId,
                    oldStatus,
                    newStatus,
                    Guid.Empty, // Actor is logged in ChangeStatusAsync
                    string.Empty,
                    userIds);
            }

        }
        catch (Exception ex)
        {
            // Don't fail the status change if broadcast fails
            _logger?.LogWarning(ex, "BroadcastTicketUpdateAsync: Failed to broadcast update for TicketId={TicketId}", ticketId);
        }
    }
}