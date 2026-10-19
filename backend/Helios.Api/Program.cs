
using Helios.Application.Services;
using Helios.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using Helios.Application.Interfaces;
using Helios.Infrastructure.Repositories;


var builder = WebApplication.CreateBuilder(args);

// Register application services
builder.Services.AddOpenApi();
builder.Services.AddScoped<IMachineRepository, MachineRepository>();
builder.Services.AddScoped<MachineService>();



var connectionString = builder.Configuration
    .GetConnectionString("HeliosDatabase")
    ?? throw new InvalidOperationException(
        "Database connection string is missing."
    );

builder.Services.AddDbContext<HeliosDbContext>(options =>
    options.UseNpgsql(connectionString));


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

app.Run();
