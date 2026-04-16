namespace Ticketing.Backend.Application.DTOs;

public class SmartAssignmentStatusResponse
{
    public bool Enabled { get; set; }
}

public class SmartAssignmentUpdateRequest
{
    public bool Enabled { get; set; }
}

public class SmartAssignmentRunRequest
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Scope { get; set; } // "unassigned" or null for all
}

public class SmartAssignmentRunResponse
{
    public int AssignedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

