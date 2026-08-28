using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Doctors.Api.Tests.Infrastructure;
using FluentAssertions;

namespace Doctors.Api.Tests.Tests;

[Collection("Doctors")]
public class DoctorProfileEndpointsTests
{
    private readonly HttpClient _client;

    public DoctorProfileEndpointsTests(DoctorsDbFixture fixture)
    {
        _client = fixture.Factory.CreateClientWithDefaultTenant();
    }

    private static object ValidProfileBody(string fullName = "Dr. Jane Doe", string specialization = "Urology", string city = "Wroclaw") => new
    {
        FullName = fullName,
        Specialization = specialization,
        City = city,
        Description = "A description",
        IsActive = true,
        PriceStationaryCents = 10000,
        PriceOnlineCents = 8000,
        ConditionsTreated = new List<string> { "Ból biodra" }
    };

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<DoctorProfileResponse> CreateProfileAsync(Guid userId, string fullName = "Dr. Jane Doe", string specialization = "Urology", string city = "Wroclaw")
    {
        var token = TestJwtTokenFactory.CreateToken(userId, "Doctor");
        var request = AuthorizedRequest(HttpMethod.Post, "/api/doctors", token);
        request.Content = JsonContent.Create(ValidProfileBody(fullName, specialization, city));
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<DoctorProfileResponse>())!;
    }

    // Authentication / authorization

    [Fact]
    public async Task Create_WithoutToken_Returns401Unauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/doctors")
        {
            Content = JsonContent.Create(ValidProfileBody())
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_AsPatientRole_Returns403Forbidden()
    {
        var token = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Patient");
        var request = AuthorizedRequest(HttpMethod.Post, "/api/doctors", token);
        request.Content = JsonContent.Create(ValidProfileBody());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_AsDoctorRole_Returns201WithPersistedProfile()
    {
        var created = await CreateProfileAsync(Guid.NewGuid());

        created.FullName.Should().Be("Dr. Jane Doe");
        created.ConditionsTreated.Should().BeEquivalentTo(new[] { "Ból biodra" });
    }

    [Fact]
    public async Task Create_SecondProfileForSameUser_Returns409Conflict()
    {
        var userId = Guid.NewGuid();
        await CreateProfileAsync(userId);

        var token = TestJwtTokenFactory.CreateToken(userId, "Doctor");
        var request = AuthorizedRequest(HttpMethod.Post, "/api/doctors", token);
        request.Content = JsonContent.Create(ValidProfileBody());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Public read access

    [Fact]
    public async Task GetById_AnonymousAccess_Returns200()
    {
        var created = await CreateProfileAsync(Guid.NewGuid());

        var response = await _client.GetAsync($"/api/doctors/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_MissingProfile_Returns404()
    {
        var response = await _client.GetAsync($"/api/doctors/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Ownership on edit

    [Fact]
    public async Task Update_ByOwner_Returns200WithUpdatedFields()
    {
        var userId = Guid.NewGuid();
        var created = await CreateProfileAsync(userId);
        var token = TestJwtTokenFactory.CreateToken(userId, "Doctor");

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{created.Id}", token);
        request.Content = JsonContent.Create(ValidProfileBody(fullName: "Dr. Jane Updated"));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<DoctorProfileResponse>();
        updated!.FullName.Should().Be("Dr. Jane Updated");
    }

    [Fact]
    public async Task Update_ByNonOwningDoctor_Returns403Forbidden()
    {
        var created = await CreateProfileAsync(Guid.NewGuid());
        var otherDoctorToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{created.Id}", otherDoctorToken);
        request.Content = JsonContent.Create(ValidProfileBody(fullName: "Hijacked"));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_MissingProfile_Returns404()
    {
        var token = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");
        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{Guid.NewGuid()}", token);
        request.Content = JsonContent.Create(ValidProfileBody());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ConditionsTreated_RoundTripsThroughRealPostgresArrayColumn()
    {
        // Regression coverage for the Postgres text[] column mapping specifically —
        // the in-memory unit tests can't catch a real ORM/column mapping mistake.
        var userId = Guid.NewGuid();
        var created = await CreateProfileAsync(userId);
        var token = TestJwtTokenFactory.CreateToken(userId, "Doctor");

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{created.Id}", token);
        request.Content = JsonContent.Create(new
        {
            FullName = "Dr. Jane Doe",
            Specialization = "Urology",
            City = "Wroclaw",
            Description = "A description",
            IsActive = true,
            PriceStationaryCents = 10000,
            PriceOnlineCents = 8000,
            ConditionsTreated = new List<string> { "Ból biodra", "Kolana szpotawe", "Endoproteza" }
        });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<DoctorProfileResponse>();
        updated!.ConditionsTreated.Should().BeEquivalentTo(new[] { "Ból biodra", "Kolana szpotawe", "Endoproteza" });

        // Re-fetch independently to confirm it was actually persisted, not just echoed back.
        var refetched = await _client.GetFromJsonAsync<DoctorProfileResponse>($"/api/doctors/{created.Id}");
        refetched!.ConditionsTreated.Should().BeEquivalentTo(new[] { "Ból biodra", "Kolana szpotawe", "Endoproteza" });
    }

    // Search filters — exercised here (not in the unit tests) because SearchAsync uses
    // EF.Functions.ILike, a Postgres-specific translation the InMemory provider can't run.

    [Fact]
    public async Task Search_BySpecialization_ReturnsOnlyMatchingDoctors()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await CreateProfileAsync(Guid.NewGuid(), fullName: $"Dr. Cardio {suffix}", specialization: $"Cardiology-{suffix}");
        await CreateProfileAsync(Guid.NewGuid(), fullName: $"Dr. Derma {suffix}", specialization: $"Dermatology-{suffix}");

        var results = await _client.GetFromJsonAsync<List<DoctorProfileResponse>>($"/api/doctors?specialization=Cardiology-{suffix}");

        results.Should().ContainSingle(d => d.FullName == $"Dr. Cardio {suffix}");
    }

    [Fact]
    public async Task Search_IsCaseInsensitive()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await CreateProfileAsync(Guid.NewGuid(), fullName: $"Dr. Case {suffix}", city: $"WROCLAW-{suffix}");

        var results = await _client.GetFromJsonAsync<List<DoctorProfileResponse>>($"/api/doctors?city=wroclaw-{suffix}");

        results.Should().ContainSingle(d => d.FullName == $"Dr. Case {suffix}");
    }

    [Fact]
    public async Task Search_NoFilters_DoesNotThrow()
    {
        var response = await _client.GetAsync("/api/doctors");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record DoctorProfileResponse(
        Guid Id, Guid UserId, string FullName, string Specialization, string City,
        string? Description, bool IsActive, int? PriceStationaryCents, int? PriceOnlineCents,
        List<string> ConditionsTreated);
}
