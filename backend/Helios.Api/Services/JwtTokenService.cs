
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Helios.Api.Contracts;
using Helios.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Helios.Api.Services;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtTokenService(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager)
    {
        _configuration = configuration;
        _userManager = userManager;
    }

    public async Task<AuthResponse> GenerateTokenAsync(
        ApplicationUser user)
    {
        // Get the JWT configuration
        var secretKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "JWT signing key is missing.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "JWT issuer is missing.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "JWT audience is missing.");

        if (!int.TryParse(
            _configuration["Jwt:ExpiresMinutes"],
            out var expiresMinutes) || expiresMinutes <= 0)
        {
            throw new InvalidOperationException(
                "JWT expiration configuration is invalid.");
        }

        // Our JWT key is stored as a Base64-encoded secret
        var keyBytes = Convert.FromBase64String(secretKey);

        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 bytes.");
        }

        var signingKey = new SymmetricSecurityKey(keyBytes);

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        // Retrieve the user's assigned roles
        var roles = await _userManager.GetRolesAsync(user);

        var email = user.Email
            ?? throw new InvalidOperationException(
                "User email is missing.");

        // Create claims for the JWT
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role));
        }

        // Create token
        var expiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var accessToken =
            new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResponse(
            AccessToken: accessToken,
            ExpiresAtUtc: expiresAt,
            Email: email,
            Roles: roles.ToArray()
        );
    }
}
