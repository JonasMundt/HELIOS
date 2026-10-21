
using Helios.Application.Constants;
using Microsoft.AspNetCore.Identity;

namespace Helios.Infrastructure.Identity;

public static class RoleSeeder
{
    public static async Task SeedAsync(
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        string[] roles =
        {
            UserRoles.Admin,
            UserRoles.Operator,
            UserRoles.Viewer
        };

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(
                new IdentityRole<Guid>(roleName));

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(error => error.Description));

                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {errors}");
            }

            Console.WriteLine($"HELIOS role created: {roleName}");
        }
    }
}
