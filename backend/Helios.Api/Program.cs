
using Helios.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// Register application services
builder.Services.AddOpenApi();
builder.Services.AddSingleton<MachineService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// System status endpoint
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

// Get all machines
app.MapGet("/api/machines", (MachineService machineService) =>
{
    return Results.Ok(machineService.GetAllMachines());
});

// Get a machine by ID
app.MapGet("/api/machines/{id:guid}",
    (Guid id, MachineService machineService) =>
{
    var machine = machineService.GetMachineById(id);

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
