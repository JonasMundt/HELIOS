
using System.Net;
using Xunit;

namespace Helios.Tests;

public class AuthorizationTests
{
    // ------------------------------------------
    // VIEWER: ACCESS DENIED
    // ------------------------------------------

    [Theory]
    [InlineData("/api/machines/00000000-0000-0000-0000-000000000001/start")]
    [InlineData("/api/machines/00000000-0000-0000-0000-000000000001/stop")]
    [InlineData("/api/simulations")]
    [InlineData("/api/simulations/00000000-0000-0000-0000-000000000001/stop")]
    [InlineData("/api/simulations/00000000-0000-0000-0000-000000000001/advance")]
    public async Task ProtectedAction_AsViewer_ReturnsForbidden(
        string endpoint)
    {
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            "Viewer");

        using var response = await client.PostAsync(
            endpoint,
            content: null);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
    }

    // ------------------------------------------
    // ANONYMOUS: AUTHENTICATION REQUIRED
    // ------------------------------------------

    [Theory]
    [InlineData("/api/machines/00000000-0000-0000-0000-000000000001/start")]
    [InlineData("/api/machines/00000000-0000-0000-0000-000000000001/stop")]
    [InlineData("/api/simulations")]
    [InlineData("/api/simulations/00000000-0000-0000-0000-000000000001/stop")]
    [InlineData("/api/simulations/00000000-0000-0000-0000-000000000001/advance")]
    public async Task ProtectedAction_WithoutAuthentication_ReturnsUnauthorized(
        string endpoint)
    {
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-Anonymous",
            "true");

        using var response = await client.PostAsync(
            endpoint,
            content: null);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    // ------------------------------------------
    // OPERATOR / ADMIN: ACCESS ALLOWED
    // ------------------------------------------

    [Theory]
    [InlineData("Operator", "/api/machines/00000000-0000-0000-0000-000000000001/start")]
    [InlineData("Operator", "/api/machines/00000000-0000-0000-0000-000000000001/stop")]
    [InlineData("Operator", "/api/simulations")]
    [InlineData("Operator", "/api/simulations/00000000-0000-0000-0000-000000000001/stop")]
    [InlineData("Operator", "/api/simulations/00000000-0000-0000-0000-000000000001/advance")]
    [InlineData("Admin", "/api/machines/00000000-0000-0000-0000-000000000001/start")]
    [InlineData("Admin", "/api/machines/00000000-0000-0000-0000-000000000001/stop")]
    [InlineData("Admin", "/api/simulations")]
    [InlineData("Admin", "/api/simulations/00000000-0000-0000-0000-000000000001/stop")]
    [InlineData("Admin", "/api/simulations/00000000-0000-0000-0000-000000000001/advance")]
    public async Task ProtectedAction_AsOperatorOrAdmin_PassesAuthorization(
        string role,
        string endpoint)
    {
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            role);

        using var response = await client.PostAsync(
            endpoint,
            content: null);

        // Authentication must succeed.
        Assert.NotEqual(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        // Role-based authorization must succeed.
        Assert.NotEqual(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        // The endpoint should not crash.
        Assert.NotEqual(
            HttpStatusCode.InternalServerError,
            response.StatusCode);
    }

}
