using System.Net.Http.Json;
using Notifications.DTOs;
using Notifications.Interfaces;

namespace Notifications.Helpers;

public class IdentityClient : IIdentityClient
{
    private readonly HttpClient _httpClient;
    public IdentityClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<UserInfoDto?> GetUserAsync(Guid userId)
    {
        var response = await _httpClient.GetAsync($"/api/auth/users/{userId}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<UserInfoDto>();
    }
}
