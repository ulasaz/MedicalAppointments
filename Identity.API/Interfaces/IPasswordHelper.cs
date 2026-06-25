using Identity.Models;

namespace Identity.Interfaces;

public interface IPasswordHelper
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string hashedPassword, string password);
}