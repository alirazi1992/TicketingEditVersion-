using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Ticketing.Backend.Tests;

/// <summary>
/// Test-time verification that EF migrations are discoverable and have no duplicate [Migration] IDs.
/// Run with: dotnet test --filter "FullyQualifiedName~MigrationDiscoveryTests"
/// </summary>
public class MigrationDiscoveryTests
{
    [Fact]
    public void No_duplicate_Migration_ids()
    {
        var assembly = typeof(Ticketing.Backend.Infrastructure.Data.AppDbContext).Assembly;
        var migrationIds = new List<string>();
        foreach (var type in assembly.GetTypes())
        {
            var attr = type.GetCustomAttribute<MigrationAttribute>();
            if (attr != null)
                migrationIds.Add(attr.Id);
        }
        var duplicates = migrationIds.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0,
            $"Duplicate [Migration] IDs found: {string.Join(", ", duplicates)}. Each migration must have a unique ID.");
    }

    [Fact]
    public void All_migrations_are_discoverable()
    {
        var assembly = typeof(Ticketing.Backend.Infrastructure.Data.AppDbContext).Assembly;
        var migrationTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<MigrationAttribute>() != null)
            .ToList();
        Assert.True(migrationTypes.Count >= 1,
            "At least one [Migration] type should be discoverable in the assembly.");
    }
}
