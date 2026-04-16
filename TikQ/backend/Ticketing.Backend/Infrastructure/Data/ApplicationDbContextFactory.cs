using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Ticketing.Backend.Infrastructure.Data;

namespace Infrastructure.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            optionsBuilder.UseSqlServer("Server=localhost,1433;Database=SimeTest;User Id=sa;Password=@123Asd@123;TrustServerCertificate=True;"
                ,builder =>
                {
                    //builder.MigrationsHistoryTable("MigrationHistory", "dbo");
                    builder.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                });

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}