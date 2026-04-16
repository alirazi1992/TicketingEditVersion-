using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Services;

/// <summary>
/// Service for broadcasting ticket updates via SignalR.
/// This enables real-time status synchronization across all dashboards.
/// </summary>
public interface ITicketHubService
{
    /// <summary>
    /// Broadcasts a ticket status update to all relevant users.
    /// Recipients include:
    /// - The ticket creator (client)
    /// - All assigned technicians
    /// - Supervisors of assigned technicians
    /// - All admins
    /// </summary>
    Task BroadcastStatusUpdateAsync(
        Guid ticketId,
        TicketStatus oldStatus,
        TicketStatus newStatus,
        Guid actorUserId,
        string actorRole,
        IEnumerable<Guid> targetUserIds);

    /// <summary>
    /// Broadcasts a general ticket update (e.g., message added, assignment changed)
    /// </summary>
    Task BroadcastTicketUpdateAsync(
        Guid ticketId,
        string updateType,
        object? metadata,
        IEnumerable<Guid> targetUserIds);
}
