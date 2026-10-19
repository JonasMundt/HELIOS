
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Helios.Domain.Entities;
using Helios.Domain.Enums;
using Helios.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Helios.Tests;

public class MachineApiTests
{
    [Fact]
    public async Task GetMachines_ShouldReturnEmptyList()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/machines");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var machines = await response.Content
            .ReadFromJsonAsync<List<MachineResponse>>();

        Assert.NotNull(machines);
        Assert.Empty(machines);
    }

    private class MachineResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public int Type { get; set; }
        public int Status { get; set; }
    }

    
    [Fact]
    public async Task StartMachine_ShouldUpdateStatusToRunning()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        Guid machineId;

        // Insert a machine into the isolated test database.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<HeliosDbContext>();

            var machine = new Machine(
                "Test Conveyor Belt",
                MachineType.ConveyorBelt);

            await dbContext.Machines.AddAsync(machine);
            await dbContext.SaveChangesAsync();

            machineId = machine.Id;
        }

        // Act
        var response = await client.PostAsync(
            $"/api/machines/{machineId}/start",
            null);

        // Assert HTTP response.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the persisted status in a fresh database context.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<HeliosDbContext>();

            var updatedMachine = await dbContext.Machines
                .FindAsync(machineId);

            Assert.NotNull(updatedMachine);
            Assert.Equal(
                MachineStatus.Running,
                updatedMachine.Status);
        }
    }

    [Fact]
    public async Task StopMachine_ShouldUpdateStatusToOffline()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        Guid machineId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<HeliosDbContext>();

            var machine = new Machine(
                "Test Scanner",
                MachineType.Scanner);

            machine.SetStatus(MachineStatus.Running);

            await dbContext.Machines.AddAsync(machine);
            await dbContext.SaveChangesAsync();

            machineId = machine.Id;
        }

        // Act
        var response = await client.PostAsync(
            $"/api/machines/{machineId}/stop",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<HeliosDbContext>();

            var updatedMachine = await dbContext.Machines
                .FindAsync(machineId);

            Assert.NotNull(updatedMachine);
            Assert.Equal(
                MachineStatus.Offline,
                updatedMachine.Status);
        }
    }


    [Fact]
    public async Task StartMachine_DuringActiveSimulation_ShouldReturnConflict()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        Guid machineId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<HeliosDbContext>();

            var machine = new Machine(
                "Test Hydraulic Press",
                MachineType.HydraulicPress);

            await dbContext.Machines.AddAsync(machine);
            await dbContext.SaveChangesAsync();

            machineId = machine.Id;
        }

        // Start a simulation.
        var simulationResponse = await client.PostAsync(
            "/api/simulations",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            simulationResponse.StatusCode);

        // Act: Attempt manual machine control.
        var response = await client.PostAsync(
            $"/api/machines/{machineId}/start",
            null);

        // Assert: Manual control must be blocked.
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        // Verify that the machine was not started.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<HeliosDbContext>();

            var machine = await dbContext.Machines
                .FindAsync(machineId);

            Assert.NotNull(machine);
            Assert.NotEqual(
                MachineStatus.Running,
                machine.Status);
        }
    }


    [Fact]
    public async Task StopMachine_DuringActiveSimulation_ShouldReturnConflict()
    {
        // Arrange
        using var factory = new HeliosWebApplicationFactory();
        using var client = factory.CreateClient();

        Guid machineId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<HeliosDbContext>();

            var machine = new Machine(
                "Test Robot Arm",
                MachineType.RobotArm);

            machine.SetStatus(MachineStatus.Running);

            await dbContext.Machines.AddAsync(machine);
            await dbContext.SaveChangesAsync();

            machineId = machine.Id;
        }

        // Start a simulation.
        var simulationResponse = await client.PostAsync(
            "/api/simulations",
            null);

        Assert.Equal(
            HttpStatusCode.Created,
            simulationResponse.StatusCode);

        // Act: Attempt to stop the machine manually.
        var response = await client.PostAsync(
            $"/api/machines/{machineId}/stop",
            null);

        // Assert: Manual control must be blocked.
        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        // Verify that the machine status has not changed.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<HeliosDbContext>();

            var machine = await dbContext.Machines
                .FindAsync(machineId);

            Assert.NotNull(machine);
            Assert.Equal(
                MachineStatus.Running,
                machine.Status);
        }
    }


}
