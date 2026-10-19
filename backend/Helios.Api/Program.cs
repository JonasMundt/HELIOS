
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

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

app.Run();
