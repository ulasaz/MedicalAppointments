using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Doctors.Api.Tests.Infrastructure;
using FluentAssertions;

namespace Doctors.Api.Tests.Tests;

[Collection("Doctors")]
public class MedicalServicesEndpointsTests
{
    private readonly HttpClient _client;

    public MedicalServicesEndpointsTests(DoctorsDbFixture fixture)
    {
        _client = fixture.Factory.CreateClientWithDefaultTenant();
    }

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<(Guid DoctorId, Guid UserId, string Token)> CreateDoctorAsync()
    {
        var userId = Guid.NewGuid();
        var token = TestJwtTokenFactory.CreateToken(userId, "Doctor");
        var request = AuthorizedRequest(HttpMethod.Post, "/api/doctors", token);
        request.Content = JsonContent.Create(new
        {
            FullName = "Dr. Jane Doe",
            Specialization = "Urology",
            City = "Wroclaw",
            Description = (string?)null,
            IsActive = true,
            PriceStationaryCents = (int?)null,
            PriceOnlineCents = (int?)null,
            ConditionsTreated = new List<string>()
        });
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var doctor = await response.Content.ReadFromJsonAsync<DoctorProfileResponse>();
        return (doctor!.Id, userId, token);
    }

    private static object ValidServiceBody(string name = "Kwalifikacja do operacji", int priceCents = 25000) => new
    {
        Name = name,
        Description = "A description",
        PriceCents = priceCents,
        AllowedVisitTypes = new[] { "Stationary", "Online" }
    };

    // Authentication / authorization

    [Fact]
    public async Task Create_WithoutToken_Returns401Unauthorized()
    {
        var (doctorId, _, _) = await CreateDoctorAsync();

        var response = await _client.PostAsJsonAsync($"/api/doctors/{doctorId}/services", ValidServiceBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_AsPatientRole_Returns403Forbidden()
    {
        var (doctorId, _, _) = await CreateDoctorAsync();
        var patientToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Patient");

        var request = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", patientToken);
        request.Content = JsonContent.Create(ValidServiceBody());
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_ByNonOwningDoctor_Returns403Forbidden()
    {
        var (doctorId, _, _) = await CreateDoctorAsync();
        var otherDoctorToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");

        var request = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", otherDoctorToken);
        request.Content = JsonContent.Create(ValidServiceBody());
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // Happy path — also a regression test for the earlier CreatedAtAction("GetForDoctorAsync", ...)
    // bug: ASP.NET Core strips the "Async" suffix from action names by default, so referencing the
    // literal C# method name there threw "No route matches the supplied values" (500) on every
    // successful create. This test fails loudly if that regresses.

    [Fact]
    public async Task Create_ByOwningDoctor_Returns201WithLocationHeaderAndPersistedService()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();

        var request = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", token);
        request.Content = JsonContent.Create(ValidServiceBody());
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<MedicalServiceResponse>();
        created!.Name.Should().Be("Kwalifikacja do operacji");
        created.PriceCents.Should().Be(25000);
        created.AllowedVisitTypes.Should().BeEquivalentTo(new[] { "Stationary", "Online" });
    }

    [Fact]
    public async Task Create_VisitTypesSerializeAsStringsNotIntegers()
    {
        // Regression coverage for the missing JsonStringEnumConverter bug — without it,
        // VisitType values round-trip as raw integers (0/1) instead of "Stationary"/"Online".
        var (doctorId, _, token) = await CreateDoctorAsync();

        var request = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", token);
        request.Content = JsonContent.Create(ValidServiceBody());
        var response = await _client.SendAsync(request);

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().Contain("\"Stationary\"");
        raw.Should().NotContain("\"allowedVisitTypes\":[0,1]");
    }

    [Fact]
    public async Task Create_EmptyAllowedVisitTypes_Returns400BadRequest()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();

        var request = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", token);
        request.Content = JsonContent.Create(new { Name = "X", Description = (string?)null, PriceCents = 100, AllowedVisitTypes = Array.Empty<string>() });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_NegativePrice_Returns400BadRequest()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();

        var request = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", token);
        request.Content = JsonContent.Create(ValidServiceBody(priceCents: -1));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Public read access

    [Fact]
    public async Task GetForDoctor_AnonymousAccess_ReturnsCreatedService()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();
        var createRequest = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", token);
        createRequest.Content = JsonContent.Create(ValidServiceBody());
        await _client.SendAsync(createRequest);

        var results = await _client.GetFromJsonAsync<List<MedicalServiceResponse>>($"/api/doctors/{doctorId}/services");

        results.Should().ContainSingle(s => s.Name == "Kwalifikacja do operacji");
    }

    [Fact]
    public async Task GetById_MissingService_Returns404()
    {
        var (doctorId, _, _) = await CreateDoctorAsync();

        var response = await _client.GetAsync($"/api/doctors/{doctorId}/services/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Update / Delete ownership

    [Fact]
    public async Task Update_ByOwner_Returns200WithUpdatedPrice()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();
        var createRequest = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", token);
        createRequest.Content = JsonContent.Create(ValidServiceBody());
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MedicalServiceResponse>();

        var updateRequest = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{doctorId}/services/{created!.Id}", token);
        updateRequest.Content = JsonContent.Create(ValidServiceBody(priceCents: 999));
        var response = await _client.SendAsync(updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<MedicalServiceResponse>();
        updated!.PriceCents.Should().Be(999);
    }

    [Fact]
    public async Task Update_ByNonOwningDoctor_Returns403Forbidden()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();
        var createRequest = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", token);
        createRequest.Content = JsonContent.Create(ValidServiceBody());
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MedicalServiceResponse>();

        var otherDoctorToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");
        var updateRequest = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{doctorId}/services/{created!.Id}", otherDoctorToken);
        updateRequest.Content = JsonContent.Create(ValidServiceBody(name: "Hijacked"));
        var response = await _client.SendAsync(updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_ByOwner_Returns204AndRemovesService()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();
        var createRequest = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", token);
        createRequest.Content = JsonContent.Create(ValidServiceBody());
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MedicalServiceResponse>();

        var deleteRequest = AuthorizedRequest(HttpMethod.Delete, $"/api/doctors/{doctorId}/services/{created!.Id}", token);
        var deleteResponse = await _client.SendAsync(deleteRequest);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await _client.GetAsync($"/api/doctors/{doctorId}/services/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ByNonOwningDoctor_Returns403Forbidden()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();
        var createRequest = AuthorizedRequest(HttpMethod.Post, $"/api/doctors/{doctorId}/services", token);
        createRequest.Content = JsonContent.Create(ValidServiceBody());
        var createResponse = await _client.SendAsync(createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<MedicalServiceResponse>();

        var otherDoctorToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");
        var deleteRequest = AuthorizedRequest(HttpMethod.Delete, $"/api/doctors/{doctorId}/services/{created!.Id}", otherDoctorToken);
        var response = await _client.SendAsync(deleteRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private record DoctorProfileResponse(Guid Id);

    private record MedicalServiceResponse(Guid Id, Guid DoctorProfileId, string Name, string? Description, int PriceCents, List<string> AllowedVisitTypes);
}
