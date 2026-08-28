using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Appointments.DTOs;
using Appointments.Interfaces;

namespace Appointments.Helpers;

public class DoctorsClient : IDoctorsClient
{
    // HttpContent.ReadFromJsonAsync() with no options implicitly uses JsonSerializerDefaults.Web
    // (camelCase, case-insensitive matching) but NOT the JsonStringEnumConverter registered for
    // MVC's own request/response formatting. We need the converter for VisitType, so we must
    // pass explicit options — starting from the Web defaults preserves the case-insensitive
    // property matching that would otherwise silently be lost (leaving list/enum properties at
    // their default values instead of throwing, which is what made this bug hard to spot).
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

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

    public async Task<DoctorInfoDto?> GetDoctorByUserIdAsync(Guid userId)
    {
        var response = await _httpClient.GetAsync($"/api/doctors/by-user/{userId}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<DoctorInfoDto>();
    }

    public async Task<MedicalServiceInfoDto?> GetServiceAsync(Guid doctorId, Guid serviceId)
    {
        var response = await _httpClient.GetAsync($"/api/doctors/{doctorId}/services/{serviceId}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<MedicalServiceInfoDto>(JsonOptions);
    }
}
