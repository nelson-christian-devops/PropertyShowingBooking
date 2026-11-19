using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PropertyShowingBooking.Infrastructure.Persistence;

namespace PropertyShowingBooking.Tests.Integration;

public sealed class PropertyShowingBookingWebApplicationFactory : WebApplicationFactory<Program>
{
    public PropertyShowingBookingWebApplicationFactory()
    {
        DatabasePath = Path.Combine(
            Path.GetTempPath(),
            $"property-showing-integration-{Guid.NewGuid():N}.db");
    }

    public string DatabasePath { get; }

    public string ConnectionString => $"Data Source={DatabasePath};Pooling=false;Default Timeout=5";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PropertyShowingBooking"] = ConnectionString
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(ConnectionString));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            foreach (var suffix in new[] { "", "-shm", "-wal" })
            {
                var path = DatabasePath + suffix;
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
