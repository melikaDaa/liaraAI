using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LiaraAI.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF Core tooling (migrations) can create an
/// <see cref="AppDbContext"/> without booting the API host. Uses a local
/// placeholder connection string; it is only used to build the model, not to
/// connect to a live database when generating migrations.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=liaraai;Username=liaraai;Password=LiaraAI_Local_2026!";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        return new AppDbContext(options);
    }
}
