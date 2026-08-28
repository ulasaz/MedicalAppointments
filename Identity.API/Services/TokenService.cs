using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identity.Interfaces;
using Identity.Models;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(User user)
    {
        var secretKey = _config["JwtSettings:Secret"] 
                        ?? throw new InvalidOperationException("JWT Secret is missing in configuration.");
        
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        // Absent entirely for the platform super-admin (TenantId == null) — everyone
        // else (Patient, Doctor, center-scoped Admin) carries their medical center's id
        // here so every other service can resolve tenant context straight from the token
        // without a lookup, via Finbuckle's claim strategy.
        if (user.TenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_config.GetValue<int>("Jwt:ExpirationInMinutes")),
            Issuer = "CuraSlot.Identity",
            Audience = "CuraSlot.Services",
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        
        return tokenHandler.WriteToken(token);
    }
}