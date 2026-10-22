
using Helios.Api.BackgroundServices;
using Helios.Api.Hubs;
using Helios.Api.Services;
using Helios.Application.Interfaces;
using Helios.Application.Services;
using Helios.Domain.Enums;
using Helios.Infrastructure.Persistence;
using Helios.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Helios.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Helios.Api.Endpoints;
using Helios.Application.Constants;

var builder = WebApplication.CreateBuilder(args);

// Register application services
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddSingleton<HeliosRealtimeNotifier>();
builder.Services.AddScoped<JwtTokenService>();

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

// --------------------------------------------------
// ASP.NET CORE IDENTITY
// --------------------------------------------------

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<HeliosDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();


// --------------------------------------------------
// JWT AUTHENTICATION
// --------------------------------------------------

// Integration tests run without local JWT user secrets.
// Their authentication configuration will be extended
// when we add dedicated authentication tests.
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddAuthentication();
}
else
{
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException(
            "JWT signing key is missing.");

    var jwtIssuer = builder.Configuration["Jwt:Issuer"]
        ?? throw new InvalidOperationException(
            "JWT issuer is missing.");

    var jwtAudience = builder.Configuration["Jwt:Audience"]
        ?? throw new InvalidOperationException(
            "JWT audience is missing.");

    var keyBytes = Convert.FromBase64String(jwtKey);

    if (keyBytes.Length < 32)
    {
        throw new InvalidOperationException(
            "JWT signing key must be at least 32 bytes.");
    }

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;

    // Allow SignalR to receive JWT tokens via query string
    // when using WebSockets or Server-Sent Events.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken =
                context.Request.Query["access_token"].ToString();

            var path = context.HttpContext.Request.Path;

            // Only accept query-string tokens for our SignalR hub.
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/hubs/helios"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };


            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,

                    ValidateAudience = true,
                    ValidAudience = jwtAudience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        new SymmetricSecurityKey(keyBytes),

                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,

                    ClockSkew = TimeSpan.FromSeconds(30),

                    RoleClaimType = "role",
                    NameClaimType = "email"
                };
        });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanOperate", policy =>
    {
        policy.RequireAuthenticatedUser();

        policy.RequireRole(
            UserRoles.Admin,
            UserRoles.Operator
        );
    });
});

var app = builder.Build();


 // --------------------------------------------------
 // OPTIONAL DATABASE INITIALIZATION
 // Intended for local Docker demonstrations
 // --------------------------------------------------

if (app.Configuration.GetValue<bool>("Helios:InitializeDatabase"))
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<HeliosDbContext>();

    // Apply pending Entity Framework Core migrations.
    await dbContext.Database.MigrateAsync();

    // Ensure required Identity roles exist.
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    await RoleSeeder.SeedAsync(roleManager);

    // Create initial machines if the table is empty.
    await DatabaseSeeder.SeedAsync(dbContext);

    app.Logger.LogInformation(
        "HELIOS database initialization completed.");
}


// Seed development database
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<HeliosDbContext>();

    await DatabaseSeeder.SeedAsync(dbContext);

    // Create default HELIOS roles
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    await RoleSeeder.SeedAsync(roleManager);


    // ------------------------------------------
    // DEVELOPMENT-ONLY OPERATOR ACCOUNT
    // ------------------------------------------

    var operatorEmail =
        app.Configuration["Development:OperatorEmail"];

    if (!string.IsNullOrWhiteSpace(operatorEmail))
    {
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var operatorUser =
            await userManager.FindByEmailAsync(operatorEmail);

        if (operatorUser is null)
        {
            throw new InvalidOperationException(
                "Development operator account was not found.");
        }

        if (!await userManager.IsInRoleAsync(
                operatorUser, UserRoles.Operator))
        {
            var result = await userManager.AddToRoleAsync(
                operatorUser, UserRoles.Operator);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not assign development operator role.");
            }
        }

        // Replace the default Viewer role with Operator.
        if (await userManager.IsInRoleAsync(
                operatorUser, UserRoles.Viewer))
        {
            var result = await userManager.RemoveFromRoleAsync(
                operatorUser, UserRoles.Viewer);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not remove development Viewer role.");
            }
        }

        app.Logger.LogInformation(
            "Development operator configured successfully.");
    }

}

 // --------------------------------------------------
 // FIRST OPERATOR BOOTSTRAP
 // Explicit local administrative command
 // --------------------------------------------------

if (args.Length > 0 && args[0] == "--bootstrap-operator")
{
    if (args.Length != 2 ||
        string.IsNullOrWhiteSpace(args[1]))
    {
        throw new ArgumentException(
            "Usage: --bootstrap-operator <email>");
    }

    using var scope = app.Services.CreateScope();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    // Bootstrap is only allowed when no privileged
    // accounts exist yet.
    var existingOperators =
        await userManager.GetUsersInRoleAsync(
            UserRoles.Operator);

    var existingAdmins =
        await userManager.GetUsersInRoleAsync(
            UserRoles.Admin);

    if (existingOperators.Count > 0 ||
        existingAdmins.Count > 0)
    {
        throw new InvalidOperationException(
            "Bootstrap refused: an Operator or Admin already exists.");
    }

    var email = args[1].Trim();

    var user = await userManager.FindByEmailAsync(email);

    if (user is null)
    {
        throw new InvalidOperationException(
            "Bootstrap refused: user not found. Register as Viewer first.");
    }

    if (!await userManager.IsInRoleAsync(
            user, UserRoles.Viewer))
    {
        throw new InvalidOperationException(
            "Bootstrap refused: user must have the Viewer role.");
    }

    // Promote the existing account without changing
    // or handling its password.
    var addResult = await userManager.AddToRoleAsync(
        user, UserRoles.Operator);

    if (!addResult.Succeeded)
    {
        throw new InvalidOperationException(
            "Could not assign the Operator role.");
    }

    var removeResult = await userManager.RemoveFromRoleAsync(
        user, UserRoles.Viewer);

    if (!removeResult.Succeeded)
    {
        throw new InvalidOperationException(
            "Operator was assigned, but Viewer removal failed. Review account roles.");
    }

    app.Logger.LogInformation(
        "HELIOS initial Operator configured for {Email}.",
        email);

    // Exit without starting the web server or worker.
    return;
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
}).RequireAuthorization();

// --------------------------------------------------
// AUTHENTICATION
// --------------------------------------------------

app.MapAuthEndpoints();

// --------------------------------------------------
// MACHINES
// --------------------------------------------------

// Get all machines
app.MapGet("/api/machines",
    async (MachineService machineService) =>
{
    var machines = await machineService.GetAllMachinesAsync();

    return Results.Ok(machines);
}).RequireAuthorization();

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
}).RequireAuthorization();

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
}).RequireAuthorization("CanOperate");

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
}).RequireAuthorization("CanOperate");

// --------------------------------------------------
// SIMULATIONS
// --------------------------------------------------

// Get all simulation runs
app.MapGet("/api/simulations",
    async (SimulationService simulationService) =>
{
    var simulations = await simulationService.GetAllAsync();

    return Results.Ok(simulations);
}).RequireAuthorization();

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
}).RequireAuthorization();

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
}).RequireAuthorization("CanOperate");

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
}).RequireAuthorization("CanOperate");

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
}).RequireAuthorization("CanOperate");

// --------------------------------------------------
// SIGNALR
// --------------------------------------------------

app.MapHub<HeliosHub>("/hubs/helios")
    .RequireAuthorization();

app.Run();

// Required for API integration tests
public partial class Program { }
