using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Identity.Api.Tests.Infrastructure;
using Identity.Helpers;

namespace Identity.Api.Tests.Tests;

[Collection("Identity")]
public class TenantsTests
{
    private readonly HttpClient _client;

    public TenantsTests(IdentityDbFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    // Seeded once per app start by AdminSeeder; TenantId stays null, marking it as the
    // platform super-admin rather than a center-scoped one.
    private Task<string> LoginAsSuperAdminAsync() => LoginAsync("admin@curaslot.local", "Admin1234!");

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<(string Token, Guid CenterId, string AdminEmail)> CreateCenterWithAdminAsync(string superAdminToken)
    {
        var slug = $"center-{Guid.NewGuid():N}";
        var adminEmail = $"{slug}-admin@example.com";
        var request = AuthorizedRequest(HttpMethod.Post, "/api/tenants", superAdminToken);
        request.Content = JsonContent.Create(new
        {
            Name = "Test Medical Center",
            Slug = slug,
            PrimaryColorHex = "#123456",
            AdminEmail = adminEmail,
            AdminPassword = "CenterAdmin123!",
            AdminDisplayName = "Center Admin"
        });
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var center = await response.Content.ReadFromJsonAsync<MedicalCenterDto>();

        var token = await LoginAsync(adminEmail, "CenterAdmin123!");
        return (token, center!.Id, adminEmail);
    }

    [Fact]
    public async Task GetAll_IsPubliclyAccessible_AndIncludesDefaultTenant()
    {
        var response = await _client.GetAsync("/api/tenants");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var centers = await response.Content.ReadFromJsonAsync<List<MedicalCenterDto>>();
        centers.Should().Contain(c => c.Id == DefaultTenantSeeder.DefaultTenantId);
    }

    [Fact]
    public async Task Create_AsSuperAdmin_CreatesCenterAndItsFirstAdmin()
    {
        var superAdminToken = await LoginAsSuperAdminAsync();

        var (_, centerId, adminEmail) = await CreateCenterWithAdminAsync(superAdminToken);

        var allCenters = await _client.GetFromJsonAsync<List<MedicalCenterDto>>("/api/tenants");
        allCenters.Should().Contain(c => c.Id == centerId);

        // The new admin can actually log in and belongs to the new center, not the platform.
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Email = adminEmail, Password = "CenterAdmin123!" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_AsCenterScopedAdmin_Returns403Forbidden()
    {
        var superAdminToken = await LoginAsSuperAdminAsync();
        var (centerAdminToken, _, _) = await CreateCenterWithAdminAsync(superAdminToken);

        var request = AuthorizedRequest(HttpMethod.Post, "/api/tenants", centerAdminToken);
        request.Content = JsonContent.Create(new
        {
            Name = "Another Center",
            Slug = $"other-{Guid.NewGuid():N}",
            PrimaryColorHex = "#000000",
            AdminEmail = $"other-{Guid.NewGuid():N}@example.com",
            AdminPassword = "SomePassword123!",
            AdminDisplayName = "Someone"
        });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_OwnCenter_AsCenterAdmin_Succeeds()
    {
        var superAdminToken = await LoginAsSuperAdminAsync();
        var (centerAdminToken, centerId, _) = await CreateCenterWithAdminAsync(superAdminToken);

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/tenants/{centerId}", centerAdminToken);
        request.Content = JsonContent.Create(new { Name = "Rebranded Center", PrimaryColorHex = "#abcdef", FontFamily = "Poppins", ButtonRadius = "rounded" });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<MedicalCenterDto>();
        updated!.Name.Should().Be("Rebranded Center");
        updated.PrimaryColorHex.Should().Be("#abcdef");
        updated.FontFamily.Should().Be("Poppins");
        updated.ButtonRadius.Should().Be("rounded");
    }

    [Fact]
    public async Task Update_WithUnsupportedFont_Returns400BadRequest()
    {
        var superAdminToken = await LoginAsSuperAdminAsync();
        var (centerAdminToken, centerId, _) = await CreateCenterWithAdminAsync(superAdminToken);

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/tenants/{centerId}", centerAdminToken);
        request.Content = JsonContent.Create(new { Name = "Center", PrimaryColorHex = "#abcdef", FontFamily = "Comic Sans MS", ButtonRadius = "pill" });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_AnotherCentersBranding_AsCenterAdmin_Returns403Forbidden()
    {
        var superAdminToken = await LoginAsSuperAdminAsync();
        var (_, centerAId, _) = await CreateCenterWithAdminAsync(superAdminToken);
        var (centerBAdminToken, _, _) = await CreateCenterWithAdminAsync(superAdminToken);

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/tenants/{centerAId}", centerBAdminToken);
        request.Content = JsonContent.Create(new { Name = "Hijacked Name", PrimaryColorHex = "#ff0000", FontFamily = "Inter", ButtonRadius = "pill" });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllUsers_AsCenterAdmin_OnlySeesOwnCentersUsers()
    {
        var superAdminToken = await LoginAsSuperAdminAsync();
        var (centerAAdminToken, centerAId, _) = await CreateCenterWithAdminAsync(superAdminToken);
        var (_, _, _) = await CreateCenterWithAdminAsync(superAdminToken);

        var patientEmail = $"center-a-patient-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = patientEmail,
            Password = "SecurePassword123!",
            DisplayName = "Center A Patient",
            Role = "Patient",
            TenantId = centerAId
        });

        var response = await _client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/api/auth/admin/users", centerAAdminToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserProfileResponse>>();
        users.Should().OnlyContain(u => u.TenantId == centerAId);
        users.Should().Contain(u => u.Email == patientEmail);
    }

    [Fact]
    public async Task GetAllUsers_AsSuperAdmin_SeesUsersAcrossAllCenters()
    {
        var superAdminToken = await LoginAsSuperAdminAsync();
        var (_, centerAId, _) = await CreateCenterWithAdminAsync(superAdminToken);
        var (_, centerBId, _) = await CreateCenterWithAdminAsync(superAdminToken);

        var response = await _client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/api/auth/admin/users", superAdminToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserProfileResponse>>();
        users.Should().Contain(u => u.TenantId == centerAId);
        users.Should().Contain(u => u.TenantId == centerBId);
    }

    private record AuthResponse(string Token);
    private record MedicalCenterDto(Guid Id, string Name, string Slug, string PrimaryColorHex, bool IsActive, DateTime CreatedAt, string FontFamily, string ButtonRadius);
    private record UserProfileResponse(Guid Id, string Email, string DisplayName, string Role, DateTime CreatedAt, bool IsActive, Guid? TenantId);
}
