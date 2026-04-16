using Microsoft.AspNetCore.SignalR;
using Ticketing.Backend.Application.Services;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Infrastructure.Hubs;

namespace Ticketing.Backend.Infrastructure.Services;

/// <summary>
/// Implementation of ITicketHubService using SignalR.
/// Broadcasts ticket updates to connected clients in real-time.
/// </summary>
public class TicketHubService : ITicketHubService
{
    private readonly IHubContext<TicketHub> _hubContext;
    private readonly ILogger<TicketHubService> _logger;

    public TicketHubService(IHubContext<TicketHub> hubContext, ILogger<TicketHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcasts a ticket status update to all relevant users.
    /// This is the core method for ensuring status synchronization.
    /// </summary>
    public async Task BroadcastStatusUpdateAsync(
        Guid ticketId,
        TicketStatus oldStatus,
        TicketStatus newStatus,
        Guid actorUserId,
        string actorRole,
        IEnumerable<Guid> targetUserIds)
    {
        var payload = new TicketStatusUpdatePayload
        {
            TicketId = ticketId,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            UpdatedAt = DateTime.UtcNow,
            ActorUserId = actorUserId,
            ActorRole = actorRole
        };

        var userIdList = targetUserIds.ToList();
        _logger.LogInformation(
            "BroadcastStatusUpdateAsync: TicketId={TicketId}, {OldStatus} → {NewStatus}, broadcasting to {UserCount} users",
            ticketId, oldStatus, newStatus, userIdList.Count);

        var tasks = new List<Task>();

        // Send to each target user's personal group
        foreach (var userId in userIdList)
        {
            tasks.Add(_hubContext.Clients.Group($"user_{userId}").SendAsync("TicketStatusUpdated", payload));
        }

        // Also broadcast to the ticket-specific group (for users viewing ticket detail)
        tasks.Add(_hubContext.Clients.Group($"ticket_{ticketId}").SendAsync("TicketStatusUpdated", payload));

        // Broadcast to admins group (they always see all tickets)
        tasks.Add(_hubContext.Clients.Group("admins").SendAsync("TicketStatusUpdated", payload));

        try
        {
            await Task.WhenAll(tasks);
            _logger.LogDebug("BroadcastStatusUpdateAsync: Successfully sent to all targets");
        }
        catch (Exception ex)
        {
            // Don't fail the status change if broadcast fails
            _logger.LogWarning(ex, "BroadcastStatusUpdateAsync: Failed to broadcast some updates. TicketId={TicketId}", ticketId);
        }
    }

    /// <summary>
    /// Broadcasts a general ticket update (e.g., message added, assignment changed)
    /// </summary>
    public async Task BroadcastTicketUpdateAsync(
        Guid ticketId,
        string updateType,
        object? metadata,
        IEnumerable<Guid> targetUserIds)
    {
        var payload = new
        {
            TicketId = ticketId,
            UpdateType = updateType,
            UpdatedAt = DateTime.UtcNow,
            Metadata = metadata
        };

        var userIdList = targetUserIds.ToList();
        _logger.LogInformation(
            "BroadcastTicketUpdateAsync: TicketId={TicketId}, Type={UpdateType}, broadcasting to {UserCount} users",
            ticketId, updateType, userIdList.Count);

        var tasks = new List<Task>();

        // Send to each target user's personal group
        foreach (var userId in userIdList)
        {
            tasks.Add(_hubContext.Clients.Group($"user_{userId}").SendAsync("TicketUpdated", payload));
        }

        // Also broadcast to the ticket-specific group
        tasks.Add(_hubContext.Clients.Group($"ticket_{ticketId}").SendAsync("TicketUpdated", payload));

        // Broadcast to admins group
        tasks.Add(_hubContext.Clients.Group("admins").SendAsync("TicketUpdated", payload));

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BroadcastTicketUpdateAsync: Failed to broadcast some updates. TicketId={TicketId}", ticketId);
        }
    }
}
