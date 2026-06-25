using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Identity.Api.Tests.Infrastructure;

namespace Identity.Api.Tests.Tests;

[Collection("Identity")]
public class AuthFlowTests
{
    private readonly HttpClient _client;

    public AuthFlowTests(IdentityDbFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_Returns200WithToken()
    {
        var email = $"test-{Guid.NewGuid()}@example.com";

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409Conflict()
    {
        var email = $"dup-{Guid.NewGuid()}@example.com";

        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "SecurePassword123!",
            DisplayName = "First User"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "AnotherPassword456!",
            DisplayName = "Second User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithEmptyEmail_Returns400BadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "",
            Password = "SecurePassword123!",
            DisplayName = "Test User"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "customer@demo.local",
            Password = "CafePassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401Unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "customer@demo.local",
            Password = "WrongPassword!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_Returns401Unauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "nobody@nowhere.com",
            Password = "SomePassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithValidToken_Returns200WithProfile()
    {
        var email = $"me-{Guid.NewGuid()}@example.com";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "SecurePassword123!",
            DisplayName = "Profile User"
        });
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        profile!.Email.Should().Be(email);
        profile.DisplayName.Should().Be("Profile User");
        profile.Role.Should().Be("Customer");
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_NewUser_HasCustomerRoleByDefault()
    {
        var email = $"role-{Guid.NewGuid()}@example.com";

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "SecurePassword123!",
            DisplayName = "Role Test User"
        });
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.Token);
        var response = await _client.SendAsync(request);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        profile!.Role.Should().Be("Customer");
    }

    private record AuthResponse(string Token);
    private record UserProfileResponse(Guid Id, string Email, string DisplayName, string Role);
}
