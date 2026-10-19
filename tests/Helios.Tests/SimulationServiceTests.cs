
using Helios.Application.Services;
using Helios.Domain.Enums;
using Xunit;

namespace Helios.Tests;

public class SimulationServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldCreateRunningSimulation()
    {
        // Arrange
        var repository = new FakeSimulationRepository();
        var service = new SimulationService(repository);

        // Act
        var simulation = await service.StartAsync();

        // Assert
        Assert.Equal(SimulationStatus.Running, simulation.Status);
        Assert.Equal(ProductionStage.Waiting, simulation.CurrentStage);
        Assert.NotNull(simulation.StartTime);
    }

    [Fact]
    public async Task StartAsync_ShouldRejectSecondActiveSimulation()
    {
        // Arrange
        var repository = new FakeSimulationRepository();
        var service = new SimulationService(repository);

        await service.StartAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync());
    }

    [Fact]
    public async Task AdvanceAsync_ShouldCompleteProductionAndCountItem()
    {
        // Arrange
        var repository = new FakeSimulationRepository();
        var service = new SimulationService(repository);

        var simulation = await service.StartAsync();

        // Act
        for (int i = 0; i < 5; i++)
        {
            await service.AdvanceAsync(simulation.Id);
        }

        // Assert
        var result = await service.GetByIdAsync(simulation.Id);

        Assert.NotNull(result);
        Assert.Equal(SimulationStatus.Completed, result.Status);
        Assert.Equal(ProductionStage.Finished, result.CurrentStage);
        Assert.Equal(1, result.ProducedItems);
        Assert.NotNull(result.EndTime);
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelRunningSimulation()
    {
        // Arrange
        var repository = new FakeSimulationRepository();
        var service = new SimulationService(repository);

        var simulation = await service.StartAsync();

        // Act
        var success = await service.CancelAsync(simulation.Id);

        // Assert
        Assert.True(success);
        Assert.Equal(SimulationStatus.Cancelled, simulation.Status);
        Assert.NotNull(simulation.EndTime);
    }

    [Fact]
    public async Task AdvanceAsync_ShouldRejectCompletedSimulation()
    {
        // Arrange
        var repository = new FakeSimulationRepository();
        var service = new SimulationService(repository);

        var simulation = await service.StartAsync();

        for (int i = 0; i < 5; i++)
        {
            await service.AdvanceAsync(simulation.Id);
        }

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AdvanceAsync(simulation.Id));
    }

    
    [Fact]
    public async Task CancelAsync_ShouldReturnFalseForUnknownSimulation()
    {
        // Arrange
        var repository = new FakeSimulationRepository();
        var service = new SimulationService(repository);

        // Act
        var success = await service.CancelAsync(Guid.NewGuid());

        // Assert
        Assert.False(success);
    }

    [Fact]
    public async Task AdvanceAsync_ShouldReturnNullForUnknownSimulation()
    {
        // Arrange
        var repository = new FakeSimulationRepository();
        var service = new SimulationService(repository);

        // Act
        var simulation = await service.AdvanceAsync(Guid.NewGuid());

        // Assert
        Assert.Null(simulation);
    }

}
