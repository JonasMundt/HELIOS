
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Helios.Tests;

public sealed class TestAuthHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Simulate an unauthenticated request when requested.
        if (Request.Headers.TryGetValue(
                "X-Test-Anonymous",
                out var anonymous) &&
            anonymous.ToString() == "true")
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Existing API tests run as Operator by default.
        var role = Request.Headers.TryGetValue(
            "X-Test-Role",
            out var roleHeader)
            ? roleHeader.ToString()
            : "Operator";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user"),
            new(ClaimTypes.Name, "HELIOS Test User"),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, Scheme);

        var principal = new ClaimsPrincipal(identity);

        var ticket = new AuthenticationTicket(
            principal,
            Scheme);

        return Task.FromResult(
            AuthenticateResult.Success(ticket));
    }
}
