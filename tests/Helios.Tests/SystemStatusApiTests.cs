
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.AspNetCore.Hosting;

namespace Helios.Tests;

public class SystemStatusApiTests
{
    [Fact]
    public async Task GetSystemStatus_ShouldReturnOnline()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "environment",
                    "Testing");
            });

        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(
            "/api/system/status");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content.ReadFromJsonAsync<SystemStatusResponse>();

        Assert.NotNull(status);
        Assert.Equal("HELIOS", status.Name);
        Assert.Equal("ONLINE", status.Status);
        Assert.Equal("0.1.0", status.Version);
    }

    private class SystemStatusResponse
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
