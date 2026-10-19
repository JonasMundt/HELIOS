
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Helios.Tests;

public class SimulationApiTests
{
    [Fact]
    public async Task StartSimulation_ShouldCreateRunningSimulation()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync(
            "/api/simulations",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var simulation = await response.Content
            .ReadFromJsonAsync<SimulationResponse>();

        Assert.NotNull(simulation);
        Assert.NotEqual(Guid.Empty, simulation.Id);
        Assert.Equal(1, simulation.Status);
        Assert.Equal(0, simulation.CurrentStage);
        Assert.Equal(0, simulation.ProducedItems);
        Assert.NotNull(simulation.StartTime);

        // Verify that the simulation can be retrieved.
        var getResponse = await client.GetAsync(
            $"/api/simulations/{simulation.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var savedSimulation = await getResponse.Content
            .ReadFromJsonAsync<SimulationResponse>();

        Assert.NotNull(savedSimulation);
        Assert.Equal(simulation.Id, savedSimulation.Id);
        Assert.Equal(1, savedSimulation.Status);
    }

    
    [Fact]
    public async Task StartSimulation_WhenAlreadyRunning_ShouldReturnConflict()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        // Start the first simulation.
        var firstResponse = await client.PostAsync(
            "/api/simulations",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            firstResponse.StatusCode);

        // Act: Try to start another simulation.
        var secondResponse = await client.PostAsync(
            "/api/simulations",
            null);

        // Assert
        Assert.Equal(
            HttpStatusCode.Conflict,
            secondResponse.StatusCode);
    }


    [Fact]
    public async Task CancelSimulation_ShouldAllowStartingNewSimulation()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        var startResponse = await client.PostAsync(
            "/api/simulations",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            startResponse.StatusCode);

        var simulation = await startResponse.Content
            .ReadFromJsonAsync<SimulationResponse>();

        Assert.NotNull(simulation);

        // Act: Cancel the running simulation.
        var cancelResponse = await client.PostAsync(
            $"/api/simulations/{simulation.Id}/stop",
            null);

        // Assert: Cancellation succeeded.
        Assert.Equal(
            HttpStatusCode.OK,
            cancelResponse.StatusCode);

        // Verify persisted simulation status.
        var getResponse = await client.GetAsync(
            $"/api/simulations/{simulation.Id}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var cancelledSimulation = await getResponse.Content
            .ReadFromJsonAsync<SimulationResponse>();

        Assert.NotNull(cancelledSimulation);
        Assert.Equal(4, cancelledSimulation.Status);
        Assert.NotNull(cancelledSimulation.EndTime);

        // A new simulation should now be allowed.
        var newStartResponse = await client.PostAsync(
            "/api/simulations",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            newStartResponse.StatusCode);
    }


    private class SimulationResponse
    {
        public Guid Id { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Status { get; set; }
        public int ProducedItems { get; set; }
        public int CurrentStage { get; set; }
    }
}
