
using Helios.Api.BackgroundServices;
using Helios.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Helios.Tests;

public class HeliosWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Disable automatic simulation processing in tests.
            var workerDescriptor = services.FirstOrDefault(
                descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType == typeof(SimulationWorker));

            if (workerDescriptor is not null)
            {
                services.Remove(workerDescriptor);
            }

            // Keep the in-memory database alive for this factory.
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<HeliosDbContext>(options =>
                options.UseSqlite(_connection));
        });
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);

        using var scope = Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<HeliosDbContext>();

        dbContext.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }

        base.Dispose(disposing);
    }
}
