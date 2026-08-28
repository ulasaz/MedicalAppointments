using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Identity.Api.Tests.Infrastructure;
using Identity.Helpers;

namespace Identity.Api.Tests.Tests;

[Collection("Identity")]
public class AdminTests
{
    private readonly HttpClient _client;

    public AdminTests(IdentityDbFixture fixture)
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

    private async Task<string> LoginAsAdminAsync() => await LoginAsync("admin@curaslot.local", "Admin1234!");

    private async Task<(string Token, Guid Id, string Email)> RegisterPatientAsync()
    {
        var email = $"admin-test-{Guid.NewGuid()}@example.com";
        const string password = "SecurePassword123!";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = password,
            DisplayName = "Admin Test Patient",
            Role = "Patient",
            TenantId = DefaultTenantSeeder.DefaultTenantId
        });
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        var meResponse = await _client.SendAsync(meRequest);
        var profile = await meResponse.Content.ReadFromJsonAsync<UserProfileResponse>();

        return (auth.Token, profile!.Id, email);
    }

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    [Fact]
    public async Task GetAllUsers_AsAdmin_ReturnsListContainingRegisteredUser()
    {
        var (_, patientId, patientEmail) = await RegisterPatientAsync();
        var adminToken = await LoginAsAdminAsync();

        var response = await _client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/api/auth/admin/users", adminToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserProfileResponse>>();
        users.Should().Contain(u => u.Id == patientId && u.Email == patientEmail);
    }

    [Fact]
    public async Task GetAllUsers_AsNonAdmin_Returns403Forbidden()
    {
        var (patientToken, _, _) = await RegisterPatientAsync();

        var response = await _client.SendAsync(AuthorizedRequest(HttpMethod.Get, "/api/auth/admin/users", patientToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SetUserStatus_DeactivateThenReactivate_LoginReflectsStatusEachTime()
    {
        var (_, patientId, patientEmail) = await RegisterPatientAsync();
        var adminToken = await LoginAsAdminAsync();

        var deactivateRequest = AuthorizedRequest(HttpMethod.Put, $"/api/auth/admin/users/{patientId}/status", adminToken);
        deactivateRequest.Content = JsonContent.Create(new { IsActive = false });
        var deactivateResponse = await _client.SendAsync(deactivateRequest);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var lockedOutLogin = await _client.PostAsJsonAsync("/api/auth/login", new { Email = patientEmail, Password = "SecurePassword123!" });
        lockedOutLogin.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var reactivateRequest = AuthorizedRequest(HttpMethod.Put, $"/api/auth/admin/users/{patientId}/status", adminToken);
        reactivateRequest.Content = JsonContent.Create(new { IsActive = true });
        var reactivateResponse = await _client.SendAsync(reactivateRequest);
        reactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var restoredLogin = await _client.PostAsJsonAsync("/api/auth/login", new { Email = patientEmail, Password = "SecurePassword123!" });
        restoredLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetUserStatus_AdminDeactivatesOwnAccount_Returns409Conflict()
    {
        var adminToken = await LoginAsAdminAsync();

        var meRequest = AuthorizedRequest(HttpMethod.Get, "/api/auth/me", adminToken);
        var meResponse = await _client.SendAsync(meRequest);
        var adminProfile = await meResponse.Content.ReadFromJsonAsync<UserProfileResponse>();

        var selfDeactivateRequest = AuthorizedRequest(HttpMethod.Put, $"/api/auth/admin/users/{adminProfile!.Id}/status", adminToken);
        selfDeactivateRequest.Content = JsonContent.Create(new { IsActive = false });
        var response = await _client.SendAsync(selfDeactivateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SetUserStatus_AsNonAdmin_Returns403Forbidden()
    {
        var (patientToken, patientId, _) = await RegisterPatientAsync();

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/auth/admin/users/{patientId}/status", patientToken);
        request.Content = JsonContent.Create(new { IsActive = false });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private record AuthResponse(string Token);
    private record UserProfileResponse(Guid Id, string Email, string DisplayName, string Role, DateTime CreatedAt, bool IsActive);
}
