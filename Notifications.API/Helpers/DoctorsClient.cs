using System.Net.Http.Json;
using Notifications.DTOs;
using Notifications.Interfaces;

namespace Notifications.Helpers;

public class DoctorsClient : IDoctorsClient
{
    private readonly HttpClient _httpClient;
    public DoctorsClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<DoctorInfoDto?> GetDoctorAsync(Guid doctorId)
    {
        var response = await _httpClient.GetAsync($"/api/doctors/{doctorId}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<DoctorInfoDto>();
    }
}
