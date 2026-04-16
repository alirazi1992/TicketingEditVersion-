using System;
using System.IO;
using Microsoft.Data.Sqlite;

var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
var dbPath = Path.Combine(repoRoot, "backend", "Ticketing.Backend", "App_Data", "ticketing.db");
if (!File.Exists(dbPath))
{
    Console.Error.WriteLine($"Database file not found: {dbPath}");
    return;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

var tables = new[]
{
    "SubcategoryFieldDefinitions",
    "TicketFieldValues",
    "CategoryFieldDefinitions"
};

foreach (var table in tables)
{
    Console.WriteLine($"Table: {table}");
    using var cmd = connection.CreateCommand();
    cmd.CommandText = $"PRAGMA table_info('{table}');";
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
    {
        Console.WriteLine($"  {reader.GetInt32(0)} | {reader.GetString(1)} | {reader.GetString(2)}");
    }
    Console.WriteLine();
}
