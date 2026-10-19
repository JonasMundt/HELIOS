
using Helios.Api.BackgroundServices;
using Helios.Api.Hubs;
using Helios.Api.Services;
using Helios.Application.Interfaces;
using Helios.Application.Services;
using Helios.Domain.Enums;
using Helios.Infrastructure.Persistence;
using Helios.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register application services
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddSingleton<HeliosRealtimeNotifier>();

builder.Services.AddScoped<IMachineRepository, MachineRepository>();
builder.Services.AddScoped<MachineService>();

builder.Services.AddScoped<ISimulationRepository, SimulationRepository>();
builder.Services.AddScoped<SimulationService>();

builder.Services.AddHostedService<SimulationWorker>();

// Configure PostgreSQL outside the testing environment
if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration
        .GetConnectionString("HeliosDatabase")
        ?? throw new InvalidOperationException(
            "Database connection string is missing."
        );

    builder.Services.AddDbContext<HeliosDbContext>(options =>
        options.UseNpgsql(connectionString));
}

var app = builder.Build();

// Seed development database
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<HeliosDbContext>();

    await DatabaseSeeder.SeedAsync(dbContext);
}

// OpenAPI in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// --------------------------------------------------
// SYSTEM
// --------------------------------------------------

app.MapGet("/api/system/status", () =>
{
    return Results.Ok(new
    {
        Name = "HELIOS",
        Version = "0.1.0",
        Status = "ONLINE",
        Description = "Industrial Digital Twin Platform"
    });
});

// --------------------------------------------------
// MACHINES
// --------------------------------------------------

// Get all machines
app.MapGet("/api/machines",
    async (MachineService machineService) =>
{
    var machines = await machineService.GetAllMachinesAsync();

    return Results.Ok(machines);
});

// Get a specific machine
app.MapGet("/api/machines/{id:guid}",
    async (
        Guid id,
        MachineService machineService) =>
{
    var machine = await machineService.GetMachineByIdAsync(id);

    if (machine is null)
    {
        return Results.NotFound(new
        {
            Message = "Machine not found."
        });
    }

    return Results.Ok(machine);
});

// Start machine manually
app.MapPost("/api/machines/{id:guid}/start",
    async (
        Guid id,
        MachineService machineService,
        ISimulationRepository simulationRepository,
        HeliosRealtimeNotifier notifier) =>
{
    // Prevent manual control during active simulations
    var activeSimulation =
        await simulationRepository.GetActiveAsync();

    if (activeSimulation is not null)
    {
        return Results.Conflict(new
        {
            Message = "Manual machine control is unavailable during an active simulation."
        });
    }

    var success = await machineService.StartMachineAsync(id);

    if (!success)
    {
        return Results.NotFound(new
        {
            Message = "Machine not found."
        });
    }

    // Notify all connected dashboards
    await notifier.NotifyMachinesChangedAsync();

    return Results.Ok(new
    {
        Message = "Machine started successfully.",
        MachineId = id
    });
});

// Stop machine manually
app.MapPost("/api/machines/{id:guid}/stop",
    async (
        Guid id,
        MachineService machineService,
        ISimulationRepository simulationRepository,
        HeliosRealtimeNotifier notifier) =>
{
    // Prevent manual control during active simulations
    var activeSimulation =
        await simulationRepository.GetActiveAsync();

    if (activeSimulation is not null)
    {
        return Results.Conflict(new
        {
            Message = "Manual machine control is unavailable during an active simulation."
        });
    }

    var success = await machineService.StopMachineAsync(id);

    if (!success)
    {
        return Results.NotFound(new
        {
            Message = "Machine not found."
        });
    }

    // Notify all connected dashboards
    await notifier.NotifyMachinesChangedAsync();

    return Results.Ok(new
    {
        Message = "Machine stopped successfully.",
        MachineId = id
    });
});

// --------------------------------------------------
// SIMULATIONS
// --------------------------------------------------

// Get all simulation runs
app.MapGet("/api/simulations",
    async (SimulationService simulationService) =>
{
    var simulations = await simulationService.GetAllAsync();

    return Results.Ok(simulations);
});

// Get a specific simulation
app.MapGet("/api/simulations/{id:guid}",
    async (
        Guid id,
        SimulationService simulationService) =>
{
    var simulation = await simulationService.GetByIdAsync(id);

    return simulation is null
        ? Results.NotFound()
        : Results.Ok(simulation);
});

// Start a new simulation
app.MapPost("/api/simulations",
    async (
        SimulationService simulationService,
        HeliosRealtimeNotifier notifier) =>
{
    try
    {
        var simulation = await simulationService.StartAsync();

        // Notify connected dashboards
        await notifier.NotifySimulationsChangedAsync();

        return Results.Created(
            $"/api/simulations/{simulation.Id}",
            simulation
        );
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new
        {
            Message = ex.Message
        });
    }
});

// Cancel a running simulation
app.MapPost("/api/simulations/{id:guid}/stop",
    async (
        Guid id,
        SimulationService simulationService,
        MachineService machineService,
        HeliosRealtimeNotifier notifier) =>
{
    try
    {
        var success = await simulationService.CancelAsync(id);

        if (!success)
        {
            return Results.NotFound(new
            {
                Message = "Simulation not found."
            });
        }

        // Reset all machines after cancellation
        await machineService.UpdateMachinesForStageAsync(
            ProductionStage.Waiting);

        // Notify connected dashboards
        await notifier.NotifySimulationsChangedAsync();
        await notifier.NotifyMachinesChangedAsync();

        return Results.Ok(new
        {
            Message = "Simulation cancelled."
        });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new
        {
            Message = ex.Message
        });
    }
});

// Advance a simulation manually
app.MapPost("/api/simulations/{id:guid}/advance",
    async (
        Guid id,
        SimulationService simulationService,
        HeliosRealtimeNotifier notifier) =>
{
    try
    {
        var simulation = await simulationService.AdvanceAsync(id);

        if (simulation is null)
        {
            return Results.NotFound(new
            {
                Message = "Simulation not found."
            });
        }

        // Notify connected dashboards
        await notifier.NotifySimulationsChangedAsync();

        return Results.Ok(simulation);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new
        {
            Message = ex.Message
        });
    }
});

// --------------------------------------------------
// SIGNALR
// --------------------------------------------------

app.MapHub<HeliosHub>("/hubs/helios");

app.Run();

// Required for API integration tests
public partial class Program { }
