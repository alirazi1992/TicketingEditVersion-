using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Application.Common;

/// <summary>
/// Landing path is role-based with supervisor override:
/// Admin → /admin
/// Technician + IsSupervisor → /supervisor
/// Technician → /technician
/// Client → /client
/// </summary>
public static class LandingPathResolver
{
    /// <summary>
    /// Landing path is role-based with supervisor override:
    /// Admin → /admin
    /// Technician + IsSupervisor → /supervisor
    /// Technician → /technician
    /// Client → /client
    /// </summary>
    public static string GetLandingPath(UserRole role, bool isSupervisor)
    {
        return role switch
        {
            UserRole.Admin => "/admin",

            // Keep this if the enum still contains Supervisor
            UserRole.Supervisor => "/supervisor",

            // Supervisor capability = Technician + IsSupervisor
            UserRole.Technician when isSupervisor => "/supervisor",
            UserRole.Technician => "/technician",

            _ => "/client"
        };
    }
}
