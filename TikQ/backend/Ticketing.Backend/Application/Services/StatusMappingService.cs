using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Services;

/// <summary>
/// Centralized service for role-based status mapping.
/// This is the SINGLE SOURCE OF TRUTH for how statuses are displayed to different user roles.
/// 
/// VISIBILITY RULES:
/// - Redo: Visible ONLY to Technician, Supervisor, Admin
///         For Client role, displayStatus = InProgress when canonicalStatus = Redo
/// - All other statuses: Visible to all roles (no mapping needed)
/// </summary>
public static class StatusMappingService
{
    /// <summary>
    /// Maps a canonical status to the display status based on user role.
    /// This function MUST be used for all status displays in DTOs and APIs.
    /// 
    /// Rules:
    /// - If role == Client AND canonicalStatus == Redo: return InProgress
    /// - Otherwise: return canonicalStatus unchanged
    /// </summary>
    /// <param name="canonicalStatus">The actual status stored in database (Ticket.Status)</param>
    /// <param name="role">The role of the user viewing the ticket</param>
    /// <returns>The status to display in UI</returns>
    public static TicketStatus MapStatusForRole(TicketStatus canonicalStatus, UserRole role)
    {
        // Redo is an internal status - clients should see InProgress instead
        if (role == UserRole.Client && canonicalStatus == TicketStatus.Redo)
        {
            return TicketStatus.InProgress;
        }
        
        // All other statuses are shown as-is to all roles
        return canonicalStatus;
    }
    
    /// <summary>
    /// Maps a canonical status to the display status based on user role string.
    /// Convenience overload that accepts role as string.
    /// </summary>
    public static TicketStatus MapStatusForRole(TicketStatus canonicalStatus, string roleString)
    {
        if (Enum.TryParse<UserRole>(roleString, ignoreCase: true, out var role))
        {
            return MapStatusForRole(canonicalStatus, role);
        }
        
        // Default to showing canonical status if role is unknown
        return canonicalStatus;
    }
    
    /// <summary>
    /// Checks if a user with the given role can see the Redo status.
    /// Clients cannot see Redo - they see InProgress instead.
    /// </summary>
    public static bool CanSeeRedoStatus(UserRole role)
    {
        return role != UserRole.Client;
    }
    
    /// <summary>
    /// Checks if a user with the given role can set the Redo status.
    /// Only Technician, Supervisor (via Technician role), and Admin can set Redo.
    /// </summary>
    public static bool CanSetRedoStatus(UserRole role)
    {
        return role == UserRole.Technician || role == UserRole.Admin;
    }
    
    /// <summary>
    /// Gets the list of statuses that should be available in dropdowns for a given role.
    /// Clients should not see Redo in status selection UI.
    /// </summary>
    public static IEnumerable<TicketStatus> GetAvailableStatusesForRole(UserRole role)
    {
        var allStatuses = Enum.GetValues<TicketStatus>();
        
        if (role == UserRole.Client)
        {
            // Clients cannot see or select Redo
            return allStatuses.Where(s => s != TicketStatus.Redo);
        }
        
        return allStatuses;
    }
}
