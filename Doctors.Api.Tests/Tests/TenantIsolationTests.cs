using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Doctors.Api.Tests.Infrastructure;
using FluentAssertions;

namespace Doctors.Api.Tests.Tests;

/// <summary>
/// The whole point of multitenancy: a medical center's doctors must be invisible to every
/// other center, from public search down to direct-by-id lookup and admin moderation. Uses
/// two arbitrary tenant identifiers (Finbuckle's EchoStore trusts whatever the claim/header
/// says) rather than the seeded default tenant, to prove isolation holds between tenants
/// neither of which is "the" default.
/// </summary>
[Collection("Doctors")]
public class TenantIsolationTests
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    private readonly HttpClient _client;

    public TenantIsolationTests(DoctorsDbFixture fixture)
    {
        // Deliberately the plain client (no baked-in default tenant header) so each request
        // below controls its own tenant context explicitly.
        _client = fixture.Factory.CreateClient();
    }

    private HttpRequestMessage TenantRequest(HttpMethod method, string url, string tenantId, string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Tenant-Id", tenantId);
        if (bearerToken != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        return request;
    }

    private async Task<Guid> CreateDoctorAsync(string tenantId, string fullName)
    {
        var token = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor", tenantId: tenantId);
        var request = TenantRequest(HttpMethod.Post, "/api/doctors", tenantId, token);
        request.Content = JsonContent.Create(new
        {
            FullName = fullName,
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
        return doctor!.Id;
    }

    [Fact]
    public async Task Search_OnlyReturnsDoctorsFromTheRequestingTenant()
    {
        var doctorAId = await CreateDoctorAsync(TenantA, "Dr. Isolation Alpha");
        var doctorBId = await CreateDoctorAsync(TenantB, "Dr. Isolation Beta");

        var searchAsTenantA = await _client.SendAsync(TenantRequest(HttpMethod.Get, "/api/doctors", TenantA));
        var doctorsInA = await searchAsTenantA.Content.ReadFromJsonAsync<List<DoctorProfileResponse>>();
        doctorsInA.Should().Contain(d => d.Id == doctorAId);
        doctorsInA.Should().NotContain(d => d.Id == doctorBId);

        var searchAsTenantB = await _client.SendAsync(TenantRequest(HttpMethod.Get, "/api/doctors", TenantB));
        var doctorsInB = await searchAsTenantB.Content.ReadFromJsonAsync<List<DoctorProfileResponse>>();
        doctorsInB.Should().Contain(d => d.Id == doctorBId);
        doctorsInB.Should().NotContain(d => d.Id == doctorAId);
    }

    [Fact]
    public async Task GetById_ForAnotherTenantsDoctor_Returns404()
    {
        var doctorBId = await CreateDoctorAsync(TenantB, "Dr. Isolation Gamma");

        var response = await _client.SendAsync(TenantRequest(HttpMethod.Get, $"/api/doctors/{doctorBId}", TenantA));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminAll_AsTenantScopedAdmin_OnlyListsOwnTenantsDoctors()
    {
        var doctorAId = await CreateDoctorAsync(TenantA, "Dr. Isolation Delta");
        var doctorBId = await CreateDoctorAsync(TenantB, "Dr. Isolation Epsilon");
        var tenantAAdminToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Admin", tenantId: TenantA);

        var response = await _client.SendAsync(TenantRequest(HttpMethod.Get, "/api/doctors/admin/all", TenantA, tenantAAdminToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doctors = await response.Content.ReadFromJsonAsync<List<DoctorProfileResponse>>();
        doctors.Should().Contain(d => d.Id == doctorAId);
        doctors.Should().NotContain(d => d.Id == doctorBId);
    }

    [Fact]
    public async Task Request_WithNoTenantResolved_Returns400()
    {
        var response = await _client.GetAsync("/api/doctors");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ClaimStrategy_TakesPriorityOverHeaderStrategy()
    {
        // The bearer token says tenant A; the header (deliberately) says tenant B. If the
        // claim strategy really is tried first, this must still only see tenant A's doctor —
        // proving an authenticated caller can't be redirected to another center's data via a
        // stray header, whether from a bug or a malicious client.
        var doctorAId = await CreateDoctorAsync(TenantA, "Dr. Isolation Zeta");
        var doctorBId = await CreateDoctorAsync(TenantB, "Dr. Isolation Eta");
        var tenantAToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor", tenantId: TenantA);

        var searchRequest = TenantRequest(HttpMethod.Get, "/api/doctors", TenantB, tenantAToken);
        var response = await _client.SendAsync(searchRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doctors = await response.Content.ReadFromJsonAsync<List<DoctorProfileResponse>>();
        doctors.Should().Contain(d => d.Id == doctorAId);
        doctors.Should().NotContain(d => d.Id == doctorBId);
    }

    private record DoctorProfileResponse(Guid Id, bool IsActive);
}
