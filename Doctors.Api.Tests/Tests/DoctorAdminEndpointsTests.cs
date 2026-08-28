using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Doctors.Api.Tests.Infrastructure;
using FluentAssertions;

namespace Doctors.Api.Tests.Tests;

[Collection("Doctors")]
public class DoctorAdminEndpointsTests
{
    private readonly HttpClient _client;

    public DoctorAdminEndpointsTests(DoctorsDbFixture fixture)
    {
        _client = fixture.Factory.CreateClientWithDefaultTenant();
    }

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<Guid> CreateDoctorAsync(bool isActive = true)
    {
        var token = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");
        var request = AuthorizedRequest(HttpMethod.Post, "/api/doctors", token);
        request.Content = JsonContent.Create(new
        {
            FullName = "Dr. Admin Test",
            Specialization = "Urology",
            City = "Wroclaw",
            Description = (string?)null,
            IsActive = isActive,
            PriceStationaryCents = (int?)null,
            PriceOnlineCents = (int?)null,
            ConditionsTreated = new List<string>()
        });
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doctor = await response.Content.ReadFromJsonAsync<DoctorProfileResponse>();
        return doctor!.Id;
    }

    [Fact]
    public async Task GetAllForAdmin_WithoutToken_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/doctors/admin/all");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllForAdmin_AsDoctorRole_Returns403Forbidden()
    {
        var token = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");

        var response = await _client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/api/doctors/admin/all", token));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllForAdmin_AsAdmin_IncludesInactiveDoctors()
    {
        var inactiveId = await CreateDoctorAsync(isActive: false);
        var adminToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Admin");

        var response = await _client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/api/doctors/admin/all", adminToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doctors = await response.Content.ReadFromJsonAsync<List<DoctorProfileResponse>>();
        doctors.Should().Contain(d => d.Id == inactiveId && !d.IsActive);
    }

    [Fact]
    public async Task SetActiveStatus_AsAdmin_TogglesDoctorVisibility()
    {
        var doctorId = await CreateDoctorAsync(isActive: true);
        var adminToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Admin");

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/admin/{doctorId}/status", adminToken);
        request.Content = JsonContent.Create(new { IsActive = false });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<DoctorProfileResponse>();
        updated!.IsActive.Should().BeFalse();

        // Deactivated doctors must disappear from the public, patient-facing search.
        var publicSearch = await _client.GetFromJsonAsync<List<DoctorProfileResponse>>($"/api/doctors?name=Dr. Admin Test");
        publicSearch.Should().NotContain(d => d.Id == doctorId);
    }

    [Fact]
    public async Task SetActiveStatus_AsDoctorRole_Returns403Forbidden()
    {
        var doctorId = await CreateDoctorAsync();
        var doctorToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/admin/{doctorId}/status", doctorToken);
        request.Content = JsonContent.Create(new { IsActive = false });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetActiveStatus_DoctorNotFound_Returns404()
    {
        var adminToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Admin");

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/admin/{Guid.NewGuid()}/status", adminToken);
        request.Content = JsonContent.Create(new { IsActive = false });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record DoctorProfileResponse(Guid Id, bool IsActive);
}
