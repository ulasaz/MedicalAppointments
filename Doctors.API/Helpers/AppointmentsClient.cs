using System.Net.Http.Json;
using Doctors.DTOs;
using Doctors.Interfaces;

namespace Doctors.Helpers;

public class AppointmentsClient : IAppointmentsClient
{
    private readonly HttpClient _httpClient;
    public AppointmentsClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<DoctorRatingDto?> GetRatingAsync(Guid doctorId)
    {
        var response = await _httpClient.GetAsync($"/api/doctors/{doctorId}/rating");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<DoctorRatingDto>();
    }

    public async Task<Dictionary<Guid, DoctorRatingDto>> GetRatingsAsync(IEnumerable<Guid> doctorIds)
    {
        var ids = doctorIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, DoctorRatingDto>();
        }

        var query = string.Join("&", ids.Select(id => $"ids={id}"));
        var response = await _httpClient.GetAsync($"/api/doctors/ratings?{query}");
        if (!response.IsSuccessStatusCode)
        {
            return new Dictionary<Guid, DoctorRatingDto>();
        }

        var results = await response.Content.ReadFromJsonAsync<List<DoctorRatingDto>>();
        return results?.ToDictionary(r => r.DoctorId) ?? new Dictionary<Guid, DoctorRatingDto>();
    }
}
