using Notifications.DTOs;

namespace Notifications.Interfaces;

public interface IIdentityClient
{
    Task<UserInfoDto?> GetUserAsync(Guid userId);
}
