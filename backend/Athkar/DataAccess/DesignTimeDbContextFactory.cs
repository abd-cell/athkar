using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Athkar.DataAccess;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without starting the web host — which
/// it would otherwise do, running the startup migration and the seeder as a side
/// effect of asking for a migration script.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DatabaseService>
{
    public DatabaseService CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var options = new DbContextOptionsBuilder<DatabaseService>()
            .UseSqlServer(configuration.GetConnectionString("Default"))
            .Options;

        return new DatabaseService(options);
    }
}
