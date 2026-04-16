using Microsoft.Extensions.Logging;
using Ticketing.Backend.Application.Repositories;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Services;

public interface ISmartAssignmentService
{
    Task<Guid?> AssignTechnicianToTicketAsync(Guid ticketId);
    Task<int> AssignUnassignedTicketsAsync(DateTime? startDate = null, DateTime? endDate = null);
}

public class SmartAssignmentService : ISmartAssignmentService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITechnicianRepository _technicianRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SmartAssignmentService> _logger;

    public SmartAssignmentService(
        ITicketRepository ticketRepository,
        ITechnicianRepository technicianRepository,
        IUnitOfWork unitOfWork,
        ILogger<SmartAssignmentService> logger)
    {
        _ticketRepository = ticketRepository;
        _technicianRepository = technicianRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Assigns a technician to a ticket using least-loaded active technician rule
    /// </summary>
    public async Task<Guid?> AssignTechnicianToTicketAsync(Guid ticketId)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId);

        if (ticket == null || ticket.TechnicianId != null)
        {
            return null; // Ticket not found or already assigned
        }

        // Get all active technicians that have a linked User account (UserId != null)
        // Technicians without UserId cannot be assigned - it would leave AssignedToUserId null
        var eligibleTechnicians = await _technicianRepository.GetActiveWithUserIdAsync();

        if (!eligibleTechnicians.Any())
        {
            return null; // No eligible technicians available (active + linked to User)
        }

        // Calculate load for each technician (count of open/in-progress tickets)
        var technicianLoads = new List<(Guid TechnicianId, int LoadCount)>();
        var activeStatuses = new[] { TicketStatus.Submitted, TicketStatus.SeenRead, TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Redo };

        foreach (var tech in eligibleTechnicians)
        {
            var loadCount = await _ticketRepository.CountByTechnicianIdAndStatusAsync(tech.Id, activeStatuses);
            technicianLoads.Add((tech.Id, loadCount));
        }

        // Select least loaded technician (tie-break by earliest created technician)
        var selectedTechnician = technicianLoads
            .OrderBy(t => t.LoadCount)
            .ThenBy(t => t.TechnicianId) // Tie-break by ID (earliest)
            .First();

        // Load technician to get UserId for AssignedToUserId
        var technician = await _technicianRepository.GetByIdAsync(selectedTechnician.TechnicianId);
        
        if (technician == null)
        {
            return null;
        }

        // CRITICAL: Technician MUST be linked to a User account for assignment to work
        // If UserId is null, skip this technician - assignment would leave ticket in broken state
        if (technician.UserId == null)
        {
            _logger.LogWarning(
                "SmartAssignment SKIPPED: Technician {TechnicianId} ({TechnicianName}) has no linked User account (UserId is null). Ticket {TicketId} remains unassigned.",
                technician.Id, technician.FullName, ticketId);
            return null; // Do NOT assign - ticket would remain unassigned in queries
        }

        // Assign technician to ticket - set BOTH TechnicianId AND AssignedToUserId for consistency
        ticket.TechnicianId = selectedTechnician.TechnicianId;
        ticket.AssignedToUserId = technician.UserId; // CRITICAL: Set to Technician.UserId for filtering/queries
        // When assigning, set status to Open (not InProgress) - technician will change to InProgress when they start working
        if (ticket.Status == TicketStatus.Submitted)
        {
            ticket.Status = TicketStatus.Open;
        }
        ticket.UpdatedAt = DateTime.UtcNow;

        await _ticketRepository.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "SmartAssignment SUCCESS: Ticket {TicketId} assigned to Technician {TechnicianId} (UserId={UserId})",
            ticketId, technician.Id, technician.UserId);

        return selectedTechnician.TechnicianId;
    }

    /// <summary>
    /// Assigns all unassigned tickets within a date range (or all if no range specified)
    /// </summary>
    public async Task<int> AssignUnassignedTicketsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var unassignedTickets = await _ticketRepository.GetUnassignedTicketsAsync(startDate, endDate);
        int assignedCount = 0;

        foreach (var ticket in unassignedTickets)
        {
            var assignedTechnicianId = await AssignTechnicianToTicketAsync(ticket.Id);
            if (assignedTechnicianId != null)
            {
                assignedCount++;
            }
        }

        return assignedCount;
    }
}

