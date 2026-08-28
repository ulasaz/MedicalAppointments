using System.Net.Http.Json;
using Appointments.DTOs;
using Appointments.Interfaces;

namespace Appointments.Helpers;

public class IdentityClient : IIdentityClient
{
    private readonly HttpClient _httpClient;
    public IdentityClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<PatientInfoDto?> GetUserAsync(Guid userId)
    {
        var response = await _httpClient.GetAsync($"/api/auth/users/{userId}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PatientInfoDto>();
    }
}
