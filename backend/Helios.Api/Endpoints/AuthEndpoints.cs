
using Helios.Api.Contracts;
using Helios.Api.Services;
using Helios.Application.Constants;
using Helios.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Helios.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        // ------------------------------------------
        // REGISTER
        // ------------------------------------------

        group.MapPost("/register", async (
            RegisterRequest request,
            UserManager<ApplicationUser> userManager,
            JwtTokenService jwtTokenService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new
                {
                    Message = "Email and password are required."
                });
            }

            var email = request.Email.Trim();

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email
            };

            // Identity validates and hashes the password.
            var result = await userManager.CreateAsync(
                user,
                request.Password);

            if (!result.Succeeded)
            {
                return Results.BadRequest(new
                {
                    Message = "Registration failed.",
                    Errors = result.Errors
                        .Select(error => error.Description)
                        .ToArray()
                });
            }

            // New users can only register as Viewer.
            var roleResult = await userManager.AddToRoleAsync(
                user,
                UserRoles.Viewer);

            if (!roleResult.Succeeded)
            {
                // Avoid leaving an account without a role.
                await userManager.DeleteAsync(user);

                return Results.Problem(
                    "User role assignment failed.",
                    statusCode: 500);
            }

            // Return an access token after registration.
            var authResponse =
                await jwtTokenService.GenerateTokenAsync(user);

            return Results.Json(
                authResponse,
                statusCode: StatusCodes.Status201Created);
        })
        .AllowAnonymous();

        // ------------------------------------------
        // LOGIN
        // ------------------------------------------

        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            JwtTokenService jwtTokenService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new
                {
                    Message = "Email and password are required."
                });
            }

            var user = await userManager.FindByEmailAsync(
                request.Email.Trim());

            if (user is null)
            {
                return Results.Unauthorized();
            }

            // Checks password and applies account lockout rules.
            var result =
                await signInManager.CheckPasswordSignInAsync(
                    user,
                    request.Password,
                    lockoutOnFailure: true);

            if (!result.Succeeded)
            {
                return Results.Unauthorized();
            }

            var authResponse =
                await jwtTokenService.GenerateTokenAsync(user);

            return Results.Ok(authResponse);
        })
        .AllowAnonymous();

        // ------------------------------------------
        // CURRENT AUTHENTICATED USER
        // ------------------------------------------

        group.MapGet("/me", (ClaimsPrincipal user) =>
        {
            var userId = user.FindFirst("sub")?.Value;
            var email = user.FindFirst("email")?.Value;

            var roles = user.FindAll("role")
                .Select(claim => claim.Value)
                .ToArray();

            return Results.Ok(new
            {
                UserId = userId,
                Email = email,
                Roles = roles
            });
        })
        .RequireAuthorization();
    }
}
