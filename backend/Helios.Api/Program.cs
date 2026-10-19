using Helios.Api.BackgroundServices;
using Helios.Application.Services;
using Helios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Helios.Application.Interfaces;
using Helios.Infrastructure.Repositories;
using Helios.Domain.Enums;


var builder = WebApplication.CreateBuilder(args);

// Register application services
builder.Services.AddOpenApi();
builder.Services.AddScoped<IMachineRepository, MachineRepository>();
builder.Services.AddScoped<MachineService>();
builder.Services.AddScoped<ISimulationRepository, SimulationRepository>();
builder.Services.AddScoped<SimulationService>();
builder.Services.AddHostedService<SimulationWorker>();


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

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<HeliosDbContext>();

        await DatabaseSeeder.SeedAsync(dbContext);
    }


    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();


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


    app.MapGet("/api/machines",
        async (MachineService machineService) =>
    {
        var machines = await machineService.GetAllMachinesAsync();

        return Results.Ok(machines);
    });

    app.MapGet("/api/machines/{id:guid}",
        async (Guid id, MachineService machineService) =>
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



    app.MapPost("/api/machines/{id:guid}/start",
        async (
            Guid id,
            MachineService machineService,
            ISimulationRepository simulationRepository) =>
    {
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

        return Results.Ok(new
        {
            Message = "Machine started successfully.",
            MachineId = id
        });
    });



    app.MapPost("/api/machines/{id:guid}/stop",
        async (
            Guid id,
            MachineService machineService,
            ISimulationRepository simulationRepository) =>
    {
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

        return Results.Ok(new
        {
            Message = "Machine stopped successfully.",
            MachineId = id
        });
    });



    // Get all simulation runs
    app.MapGet("/api/simulations",
        async (SimulationService simulationService) =>
    {
        var simulations = await simulationService.GetAllAsync();

        return Results.Ok(simulations);
    });

    // Get a specific simulation
    app.MapGet("/api/simulations/{id:guid}",
        async (Guid id, SimulationService simulationService) =>
    {
        var simulation = await simulationService.GetByIdAsync(id);

        return simulation is null
            ? Results.NotFound()
            : Results.Ok(simulation);
    });

    // Start a new simulation
    app.MapPost("/api/simulations",
        async (SimulationService simulationService) =>
    {
        try
        {
            var simulation = await simulationService.StartAsync();

            return Results.Created(
                $"/api/simulations/{simulation.Id}",
                simulation
            );
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { Message = ex.Message });
        }
    });


    // Cancel a running simulation
    app.MapPost("/api/simulations/{id:guid}/stop",
        async (
            Guid id,
            SimulationService simulationService,
            MachineService machineService) =>
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

    app.MapPost("/api/simulations/{id:guid}/advance",
        async (Guid id, SimulationService simulationService) =>
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


app.Run();
public partial class Program { }
