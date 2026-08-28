using Identity.DTO_s;
using Identity.Models;

namespace Identity.Interfaces;

public interface IIdentityService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    
    Task<UserProfileResponse> GetProfileAsync(Guid userId);
    Task<List<UserProfileResponse>> GetAllUsersAsync(Guid requestingAdminId);
    Task<UserProfileResponse> SetUserActiveStatusAsync(Guid requestingAdminId, Guid targetUserId, bool isActive);
    Task<UserProfileResponse> UpdateDisplayNameAsync(Guid userId, string displayName);
}