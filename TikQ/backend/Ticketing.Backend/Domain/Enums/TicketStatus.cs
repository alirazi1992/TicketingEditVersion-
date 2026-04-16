namespace Ticketing.Backend.Domain.Enums;

/// <summary>
/// Canonical ticket status values stored in database.
/// IMPORTANT: Use StatusMappingService.MapStatusForRole() to get displayStatus for UI.
/// 
/// Status Flow:
/// Submitted -> SeenRead -> Open -> InProgress -> Solved
///                                      |
///                                      v
///                                    Redo -> InProgress (cycle back)
/// 
/// Visibility Rules:
/// - Redo: Only visible to Technician/Supervisor/Admin. Clients see "InProgress" instead.
/// - All other statuses: Visible to all roles.
/// </summary>
public enum TicketStatus
{
    /// <summary>New ticket, just created by client</summary>
    Submitted = 0,
    
    /// <summary>Ticket has been seen/read by a technician or admin (first view event)</summary>
    SeenRead = 1,
    
    /// <summary>Ticket is open and ready for work</summary>
    Open = 2,
    
    /// <summary>Work is actively being done on the ticket</summary>
    InProgress = 3,
    
    /// <summary>Ticket has been solved/answered (terminal status)</summary>
    Solved = 4,
    
    /// <summary>Ticket needs rework (internal status - clients see InProgress)</summary>
    Redo = 5
}
