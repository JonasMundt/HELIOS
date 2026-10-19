
using Helios.Domain.Entities;
using Helios.Domain.Enums;
using Xunit;

namespace Helios.Tests;

public class SimulationRunTests
{
    [Fact]
    public void NewSimulation_ShouldStartAsPending()
    {
        // Arrange & Act
        var simulation = new SimulationRun();

        // Assert
        Assert.Equal(SimulationStatus.Pending, simulation.Status);
        Assert.Equal(ProductionStage.Waiting, simulation.CurrentStage);
        Assert.Equal(0, simulation.ProducedItems);
        Assert.Null(simulation.StartTime);
        Assert.Null(simulation.EndTime);
    }

    [Fact]
    public void Start_ShouldChangeStatusToRunning()
    {
        // Arrange
        var simulation = new SimulationRun();

        // Act
        simulation.Start();

        // Assert
        Assert.Equal(SimulationStatus.Running, simulation.Status);
        Assert.NotNull(simulation.StartTime);
        Assert.Null(simulation.EndTime);
    }

    [Fact]
    public void Cancel_ShouldChangeStatusToCancelled()
    {
        // Arrange
        var simulation = new SimulationRun();
        simulation.Start();

        // Act
        simulation.Cancel();

        // Assert
        Assert.Equal(SimulationStatus.Cancelled, simulation.Status);
        Assert.NotNull(simulation.EndTime);
    }


    [Fact]
    public void AdvanceStage_ShouldMoveToNextStage()
    {
        // Arrange
        var simulation = new SimulationRun();
        simulation.Start();

        // Act
        simulation.AdvanceStage(ProductionStage.ConveyorBelt);

        // Assert
        Assert.Equal(
            ProductionStage.ConveyorBelt,
            simulation.CurrentStage);
    }

    [Fact]
    public void AdvanceStage_ShouldRejectSkippedStages()
    {
        // Arrange
        var simulation = new SimulationRun();
        simulation.Start();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            simulation.AdvanceStage(ProductionStage.Pressing));
    }

    [Fact]
    public void AdvanceStage_ShouldRejectWhenNotRunning()
    {
        // Arrange
        var simulation = new SimulationRun();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            simulation.AdvanceStage(ProductionStage.ConveyorBelt));
    }
    
    [Fact]
    public void Complete_ShouldFinishSimulation()
    {
        // Arrange
        var simulation = new SimulationRun();
        simulation.Start();

        // Act
        simulation.Complete();

        // Assert
        Assert.Equal(SimulationStatus.Completed, simulation.Status);
        Assert.NotNull(simulation.EndTime);
    }

    [Fact]
    public void FullProductionCycle_ShouldProduceOneItem()
    {
        // Arrange
        var simulation = new SimulationRun();
        simulation.Start();

        // Act
        simulation.AdvanceStage(ProductionStage.ConveyorBelt);
        simulation.AdvanceStage(ProductionStage.Scanning);
        simulation.AdvanceStage(ProductionStage.Pressing);
        simulation.AdvanceStage(ProductionStage.RobotArm);
        simulation.AdvanceStage(ProductionStage.Finished);

        simulation.AddProducedItem();
        simulation.Complete();

        // Assert
        Assert.Equal(ProductionStage.Finished, simulation.CurrentStage);
        Assert.Equal(SimulationStatus.Completed, simulation.Status);
        Assert.Equal(1, simulation.ProducedItems);
        Assert.NotNull(simulation.EndTime);
    }

}
