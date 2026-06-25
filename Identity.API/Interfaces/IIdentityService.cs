using Identity.DTO_s;
using Identity.Models;

namespace Identity.Interfaces;

public interface IIdentityService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    
    Task<UserProfileResponse> GetProfileAsync(Guid userId);
}