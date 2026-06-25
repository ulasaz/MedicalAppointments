using Identity.Models;

namespace Identity.Interfaces;

public interface ITokenService
{
    string GenerateToken(User user);
}