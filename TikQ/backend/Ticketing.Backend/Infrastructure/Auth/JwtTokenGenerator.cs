using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Ticketing.Backend.Domain.Entities;
using Ticketing.Backend.Domain.Enums;

namespace Ticketing.Backend.Infrastructure.Auth;

public interface IJwtTokenGenerator
{
    /// <param name="expirationMinutes">Override for token lifetime; null = use JwtSettings.ExpirationMinutes (e.g. 30 for login cookie).</param>
    string GenerateToken(User user, bool isSupervisor, int? expirationMinutes = null);
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(JwtSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// SECURITY-CRITICAL: Generates JWT token with role claim from TikQ user only.
    /// TikQ roles are authoritative. External directory is identity-only.
    ///
    /// Role Claim Rules (ENFORCED STRICTLY):
    /// 1. Role claim is ALWAYS derived from user.Role (TikQ Users.Role - persisted database value)
    /// 2. NEVER use roles from Company Directory or any external source
    /// 3. NEVER hardcodes roles (no "Client", "Admin", etc. as string literals)
    /// 4. NEVER applies defaults (no fallback to Client or any role)
    /// 5. Uses user.Role.ToString() to get exact enum string representation
    ///
    /// CRITICAL: This method trusts the database - user.Role must be valid when persisted
    /// Minimal claims only: sub, email, role, isSupervisor, is_supervisor (no name, no permissions).
    /// </summary>
    public string GenerateToken(User user, bool isSupervisor, int? expirationMinutes = null)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = expirationMinutes ?? _settings.ExpirationMinutes;

        // Minimal claims: sub (userId), email, ClaimTypes.Role, isSupervisor, is_supervisor only
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("isSupervisor", isSupervisor ? "true" : "false"),
            new("is_supervisor", isSupervisor ? "true" : "false")
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
