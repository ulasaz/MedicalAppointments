using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Doctors.Api.Tests.Infrastructure;

/// <summary>
/// Mints JWTs identical in shape to Identity.API's TokenService (same issuer/audience/claims),
/// signed with the same hardcoded secret Doctors.API's Program.cs validates against — so tests
/// can authenticate against Doctors.API's real auth pipeline without running Identity.API.
/// </summary>
public static class TestJwtTokenFactory
{
    private const string Secret = "MySuperSecretKeyThatIsVeryLongAndSecureForCuraSlotSystem";

    /// <summary>Matches Identity.API's DefaultTenantSeeder.DefaultTenantId — the well-known
    /// tenant every test user belongs to unless a test explicitly simulates a second tenant.</summary>
    public const string DefaultTenantId = "00000000-0000-0000-0000-000000000001";

    public static string CreateToken(Guid userId, string role, string displayName = "Test User", string email = "test@example.com", string? tenantId = DefaultTenantId)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Name, displayName),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Role, role)
        };

        if (tenantId != null)
        {
            claims.Add(new Claim("tenant_id", tenantId));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(60),
            Issuer = "CuraSlot.Identity",
            Audience = "CuraSlot.Services",
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(tokenDescriptor));
    }
}
