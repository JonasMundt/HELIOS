
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Helios.Tests;

public class SystemStatusApiTests
{
    [Fact]
    public async Task GetSystemStatus_ShouldReturnOnline()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(
            "/api/system/status");

        // Assert
        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var status = await response.Content
            .ReadFromJsonAsync<SystemStatusResponse>();

        Assert.NotNull(status);
        Assert.Equal("HELIOS", status.Name);
        Assert.Equal("ONLINE", status.Status);
        Assert.Equal("0.1.0", status.Version);
    }


    // ------------------------------------------
    // UNAUTHENTICATED ACCESS
    // ------------------------------------------

    [Fact]
    public async Task GetSystemStatus_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        // Simulate a request without authentication.
        client.DefaultRequestHeaders.Add(
            "X-Test-Anonymous",
            "true");

        // Act
        using var response = await client.GetAsync(
            "/api/system/status");

        // Assert
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }


    private class SystemStatusResponse
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
