using Microsoft.EntityFrameworkCore;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;
using Ticketing.Backend.Infrastructure.Data;

namespace Ticketing.Backend.Application.Services;

public interface IAutomationCoverageService
{
    Task<CoverageSummaryDto> GetCoverageSummaryAsync();
    Task<CoverageBreakdownDto> GetCoverageBreakdownAsync();
    Task<CoverageGraphDto> GetCoverageGraphAsync();
}

#region DTOs

public class CoverageSummaryDto
{
    public int TotalCategories { get; set; }
    public int TotalSubcategories { get; set; }
    public int TotalPairs { get; set; }
    public int CoveredPairsCount { get; set; }
    public int UncoveredPairsCount { get; set; }
    public double CoveragePercent { get; set; }
    public int TechniciansCount { get; set; }
    public int TicketsLast30Days { get; set; }
    public int AutoAssignedLast30Days { get; set; }
    public int UnassignedLast30Days { get; set; }
}

public class CoverageBreakdownDto
{
    public List<CategorySubcategoryPairDto> UncoveredPairs { get; set; } = new();
    public List<CoveredPairDto> CoveredPairs { get; set; } = new();
    public List<TechnicianCoverageDto> TechnicianCoverage { get; set; } = new();
}

public class CategorySubcategoryPairDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int SubcategoryId { get; set; }
    public string SubcategoryName { get; set; } = string.Empty;
}

public class CoveredPairDto : CategorySubcategoryPairDto
{
    public int TechnicianCount { get; set; }
}

public class TechnicianCoverageDto
{
    public Guid TechnicianId { get; set; }
    public Guid TechnicianUserId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int CoveredPairsCount { get; set; }
    public List<CategorySubcategoryPairDto> Pairs { get; set; } = new();
}

public class CoverageGraphDto
{
    public List<GraphNodeDto> Nodes { get; set; } = new();
    public List<GraphEdgeDto> Edges { get; set; } = new();
}

public class GraphNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "category", "subcategory", "technician"
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool HasCoverage { get; set; } = true;
}

public class GraphEdgeDto
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "contains", "covers"
}

#endregion

public class AutomationCoverageService : IAutomationCoverageService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AutomationCoverageService> _logger;

    public AutomationCoverageService(AppDbContext context, ILogger<AutomationCoverageService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CoverageSummaryDto> GetCoverageSummaryAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        // Get category/subcategory counts
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .CountAsync();

        var subcategories = await _context.Subcategories
            .AsNoTracking()
            .Include(s => s.Category)
            .Where(s => s.Category != null && s.Category.IsActive)
            .ToListAsync();

        var totalSubcategories = subcategories.Count;
        var totalPairs = totalSubcategories; // Each subcategory is a "pair" (category implied)

        // Get covered subcategories (have at least 1 technician permission)
        var coveredSubcategoryIds = await _context.TechnicianSubcategoryPermissions
            .AsNoTracking()
            .Include(p => p.Technician)
            .Where(p => p.Technician != null && p.Technician.IsActive)
            .Select(p => p.SubcategoryId)
            .Distinct()
            .ToListAsync();

        var coveredPairsCount = subcategories.Count(s => coveredSubcategoryIds.Contains(s.Id));
        var uncoveredPairsCount = totalPairs - coveredPairsCount;
        var coveragePercent = totalPairs > 0 ? Math.Round((double)coveredPairsCount / totalPairs * 100, 1) : 0;

        // Get technician count (active with user accounts)
        var techniciansCount = await _context.Technicians
            .AsNoTracking()
            .Where(t => t.IsActive && t.UserId != null)
            .CountAsync();

        // Get ticket stats for last 30 days
        var ticketsLast30Days = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.CreatedAt >= cutoff)
            .CountAsync();

        // Auto-assigned = tickets that have at least one technician assignment
        var autoAssignedLast30Days = await _context.Tickets
            .AsNoTracking()
            .Where(t => t.CreatedAt >= cutoff)
            .Where(t => t.AssignedTechnicians.Any())
            .CountAsync();

        // Unassigned = tickets with no assignments (could be due to missing coverage)
        var unassignedLast30Days = ticketsLast30Days - autoAssignedLast30Days;

        return new CoverageSummaryDto
        {
            TotalCategories = categories,
            TotalSubcategories = totalSubcategories,
            TotalPairs = totalPairs,
            CoveredPairsCount = coveredPairsCount,
            UncoveredPairsCount = uncoveredPairsCount,
            CoveragePercent = coveragePercent,
            TechniciansCount = techniciansCount,
            TicketsLast30Days = ticketsLast30Days,
            AutoAssignedLast30Days = autoAssignedLast30Days,
            UnassignedLast30Days = unassignedLast30Days
        };
    }

    public async Task<CoverageBreakdownDto> GetCoverageBreakdownAsync()
    {
        // Get all subcategories with their categories
        var subcategories = await _context.Subcategories
            .AsNoTracking()
            .Include(s => s.Category)
            .Where(s => s.Category != null && s.Category.IsActive)
            .OrderBy(s => s.Category!.Name)
            .ThenBy(s => s.Name)
            .ToListAsync();

        // Get all permissions with technician info
        var permissions = await _context.TechnicianSubcategoryPermissions
            .AsNoTracking()
            .Include(p => p.Technician)
                .ThenInclude(t => t!.User)
            .Include(p => p.Subcategory)
                .ThenInclude(s => s!.Category)
            .Where(p => p.Technician != null && p.Technician.IsActive)
            .ToListAsync();

        // Group permissions by subcategory
        var permissionsBySubcategory = permissions
            .GroupBy(p => p.SubcategoryId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var uncoveredPairs = new List<CategorySubcategoryPairDto>();
        var coveredPairs = new List<CoveredPairDto>();

        foreach (var sub in subcategories)
        {
            var pair = new CategorySubcategoryPairDto
            {
                CategoryId = sub.CategoryId,
                CategoryName = sub.Category?.Name ?? "N/A",
                SubcategoryId = sub.Id,
                SubcategoryName = sub.Name
            };

            if (permissionsBySubcategory.TryGetValue(sub.Id, out var perms) && perms.Count > 0)
            {
                coveredPairs.Add(new CoveredPairDto
                {
                    CategoryId = pair.CategoryId,
                    CategoryName = pair.CategoryName,
                    SubcategoryId = pair.SubcategoryId,
                    SubcategoryName = pair.SubcategoryName,
                    TechnicianCount = perms.Count
                });
            }
            else
            {
                uncoveredPairs.Add(pair);
            }
        }

        // Build technician coverage list
        var technicianCoverage = permissions
            .GroupBy(p => p.TechnicianId)
            .Select(g =>
            {
                var tech = g.First().Technician;
                return new TechnicianCoverageDto
                {
                    TechnicianId = g.Key,
                    TechnicianUserId = tech?.UserId ?? Guid.Empty,
                    TechnicianName = tech?.User?.FullName ?? tech?.FullName ?? "Unknown",
                    IsActive = tech?.IsActive ?? false,
                    CoveredPairsCount = g.Count(),
                    Pairs = g.Select(p => new CategorySubcategoryPairDto
                    {
                        CategoryId = p.Subcategory?.CategoryId ?? 0,
                        CategoryName = p.Subcategory?.Category?.Name ?? "N/A",
                        SubcategoryId = p.SubcategoryId,
                        SubcategoryName = p.Subcategory?.Name ?? "N/A"
                    }).OrderBy(x => x.CategoryName).ThenBy(x => x.SubcategoryName).ToList()
                };
            })
            .OrderBy(t => t.TechnicianName)
            .ToList();

        return new CoverageBreakdownDto
        {
            UncoveredPairs = uncoveredPairs,
            CoveredPairs = coveredPairs,
            TechnicianCoverage = technicianCoverage
        };
    }

    public async Task<CoverageGraphDto> GetCoverageGraphAsync()
    {
        var nodes = new List<GraphNodeDto>();
        var edges = new List<GraphEdgeDto>();

        // Get categories
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        foreach (var cat in categories)
        {
            nodes.Add(new GraphNodeDto
            {
                Id = $"cat_{cat.Id}",
                Type = "category",
                Label = cat.Name,
                IsActive = cat.IsActive,
                HasCoverage = true // Categories don't have direct coverage
            });
        }

        // Get subcategories with their category relationships
        var subcategories = await _context.Subcategories
            .AsNoTracking()
            .Include(s => s.Category)
            .Where(s => s.Category != null && s.Category.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

        // Get covered subcategory IDs
        var coveredSubcategoryIds = await _context.TechnicianSubcategoryPermissions
            .AsNoTracking()
            .Include(p => p.Technician)
            .Where(p => p.Technician != null && p.Technician.IsActive)
            .Select(p => p.SubcategoryId)
            .Distinct()
            .ToListAsync();

        foreach (var sub in subcategories)
        {
            var hasCoverage = coveredSubcategoryIds.Contains(sub.Id);
            nodes.Add(new GraphNodeDto
            {
                Id = $"sub_{sub.Id}",
                Type = "subcategory",
                Label = sub.Name,
                IsActive = true,
                HasCoverage = hasCoverage
            });

            // Edge: category -> subcategory
            edges.Add(new GraphEdgeDto
            {
                Source = $"cat_{sub.CategoryId}",
                Target = $"sub_{sub.Id}",
                Type = "contains"
            });
        }

        // Get technicians with their permissions
        var permissions = await _context.TechnicianSubcategoryPermissions
            .AsNoTracking()
            .Include(p => p.Technician)
                .ThenInclude(t => t!.User)
            .Where(p => p.Technician != null && p.Technician.IsActive)
            .ToListAsync();

        // Add technician nodes
        var technicianIds = permissions.Select(p => p.TechnicianId).Distinct();
        foreach (var techId in technicianIds)
        {
            var tech = permissions.First(p => p.TechnicianId == techId).Technician;
            nodes.Add(new GraphNodeDto
            {
                Id = $"tech_{techId}",
                Type = "technician",
                Label = tech?.User?.FullName ?? tech?.FullName ?? "Unknown",
                IsActive = tech?.IsActive ?? false,
                HasCoverage = true
            });
        }

        // Add coverage edges: subcategory -> technician
        foreach (var perm in permissions)
        {
            edges.Add(new GraphEdgeDto
            {
                Source = $"sub_{perm.SubcategoryId}",
                Target = $"tech_{perm.TechnicianId}",
                Type = "covers"
            });
        }

        return new CoverageGraphDto
        {
            Nodes = nodes,
            Edges = edges
        };
    }
}














