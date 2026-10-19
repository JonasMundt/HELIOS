
using Helios.Domain.Entities;
using Helios.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Helios.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(HeliosDbContext context)
    {
        // Avoid creating duplicate machines
        if (await context.Machines.AnyAsync())
        {
            return;
        }

        var machines = new[]
        {
            new Machine(
                "Conveyor Belt 01",
                MachineType.ConveyorBelt
            ),
            new Machine(
                "Scanner Station 01",
                MachineType.Scanner
            ),
            new Machine(
                "Hydraulic Press 01",
                MachineType.HydraulicPress
            ),
            new Machine(
                "Robot Arm 01",
                MachineType.RobotArm
            )
        };

        await context.Machines.AddRangeAsync(machines);
        await context.SaveChangesAsync();
    }
}
