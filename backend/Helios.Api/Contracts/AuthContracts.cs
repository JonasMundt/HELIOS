
namespace Helios.Api.Contracts;

// Data required to register a new user
public sealed record RegisterRequest(
    string Email,
    string Password
);

// Data required to log in
public sealed record LoginRequest(
    string Email,
    string Password
);

// Data returned after successful authentication
public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string Email,
    string[] Roles
);
