using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Ticketing.Backend.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub for real-time ticket updates.
/// This hub enables instant status synchronization across all dashboards:
/// - Client dashboard
/// - Technician dashboard
/// - Supervisor dashboard
/// - Admin dashboard
/// 
/// When a ticket status changes, ALL connected users who have access to that ticket
/// will receive the update instantly without needing to refresh.
/// </summary>
[Authorize]
public class TicketHub : Hub
{
    private readonly ILogger<TicketHub> _logger;

    public TicketHub(ILogger<TicketHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// Adds the user to their personal group for targeted notifications.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            // Add user to their personal group for targeted notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            
            // Add admins to a special group that receives all ticket updates
            if (userRole == "Admin")
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
            }
            
            _logger.LogInformation("TicketHub: User {UserId} ({Role}) connected. ConnectionId={ConnectionId}",
                userId, userRole, Context.ConnectionId);
        }
        
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Logs exception details to help diagnose 1006 / WebSocket closes.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (exception != null)
        {
            _logger.LogWarning(
                "TicketHub: User {UserId} disconnected with error. ConnectionId={ConnectionId}, ExceptionType={ExceptionType}, Message={Message}, InnerMessage={InnerMessage}",
                userId ?? "(anonymous)",
                Context.ConnectionId,
                exception.GetType().Name,
                exception.Message,
                exception.InnerException?.Message ?? "(none)");
        }
        else if (!string.IsNullOrEmpty(userId))
        {
            _logger.LogInformation(
                "TicketHub: User {UserId} disconnected. ConnectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to updates for a specific ticket.
    /// Called by frontend when viewing a ticket detail page.
    /// </summary>
    public async Task SubscribeToTicket(string ticketId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket_{ticketId}");
        
        _logger.LogDebug("TicketHub: User {UserId} subscribed to ticket {TicketId}", userId, ticketId);
    }

    /// <summary>
    /// Unsubscribe from updates for a specific ticket.
    /// Called by frontend when leaving a ticket detail page.
    /// </summary>
    public async Task UnsubscribeFromTicket(string ticketId)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket_{ticketId}");
        
        _logger.LogDebug("TicketHub: User {UserId} unsubscribed from ticket {TicketId}", userId, ticketId);
    }
}

/// <summary>
/// Payload for ticket status update notifications
/// </summary>
public class TicketStatusUpdatePayload
{
    public Guid TicketId { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorRole { get; set; } = string.Empty;
}
