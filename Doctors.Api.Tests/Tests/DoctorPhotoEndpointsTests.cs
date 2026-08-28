using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Doctors.Api.Tests.Infrastructure;
using FluentAssertions;

namespace Doctors.Api.Tests.Tests;

[Collection("Doctors")]
public class DoctorPhotoEndpointsTests
{
    private readonly HttpClient _client;

    public DoctorPhotoEndpointsTests(DoctorsDbFixture fixture)
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

    private static MultipartFormDataContent PhotoContent(byte[] bytes, string contentType = "image/png", string fieldName = "file")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, fieldName, "photo.png");
        return content;
    }

    [Fact]
    public async Task Upload_WithoutToken_Returns401Unauthorized()
    {
        var (doctorId, _, _) = await CreateDoctorAsync();

        var response = await _client.PutAsync($"/api/doctors/{doctorId}/photo", PhotoContent(new byte[] { 1, 2, 3 }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_ByNonOwningDoctor_Returns403Forbidden()
    {
        var (doctorId, _, _) = await CreateDoctorAsync();
        var otherToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{doctorId}/photo", otherToken);
        request.Content = PhotoContent(new byte[] { 1, 2, 3 });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_DisallowedContentType_Returns400BadRequest()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{doctorId}/photo", token);
        request.Content = PhotoContent(new byte[] { 1, 2, 3 }, contentType: "application/pdf");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_TooLarge_Returns400BadRequest()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();
        var oversized = new byte[2 * 1024 * 1024 + 1];

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{doctorId}/photo", token);
        request.Content = PhotoContent(oversized);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_ByOwner_Returns204_AndProfileThenReportsHasPhoto()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();

        var request = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{doctorId}/photo", token);
        request.Content = PhotoContent(new byte[] { 1, 2, 3, 4 });
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var profile = await _client.GetFromJsonAsync<DoctorProfileResponse>($"/api/doctors/{doctorId}");
        profile!.HasPhoto.Should().BeTrue();
    }

    [Fact]
    public async Task GetPhoto_AfterUpload_AnonymousAccess_ReturnsBytesWithContentType()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();
        var bytes = new byte[] { 5, 6, 7, 8 };
        var uploadRequest = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{doctorId}/photo", token);
        uploadRequest.Content = PhotoContent(bytes, contentType: "image/png");
        await _client.SendAsync(uploadRequest);

        var response = await _client.GetAsync($"/api/doctors/{doctorId}/photo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        var received = await response.Content.ReadAsByteArrayAsync();
        received.Should().BeEquivalentTo(bytes);
    }

    [Fact]
    public async Task GetPhoto_NoPhotoUploaded_Returns404()
    {
        var (doctorId, _, _) = await CreateDoctorAsync();

        var response = await _client.GetAsync($"/api/doctors/{doctorId}/photo");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ByOwner_Returns204_AndPhotoNoLongerAvailable()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();
        var uploadRequest = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{doctorId}/photo", token);
        uploadRequest.Content = PhotoContent(new byte[] { 1, 2, 3 });
        await _client.SendAsync(uploadRequest);

        var deleteRequest = AuthorizedRequest(HttpMethod.Delete, $"/api/doctors/{doctorId}/photo", token);
        var deleteResponse = await _client.SendAsync(deleteRequest);

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await _client.GetAsync($"/api/doctors/{doctorId}/photo");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ByNonOwningDoctor_Returns403Forbidden()
    {
        var (doctorId, _, token) = await CreateDoctorAsync();
        var uploadRequest = AuthorizedRequest(HttpMethod.Put, $"/api/doctors/{doctorId}/photo", token);
        uploadRequest.Content = PhotoContent(new byte[] { 1, 2, 3 });
        await _client.SendAsync(uploadRequest);

        var otherToken = TestJwtTokenFactory.CreateToken(Guid.NewGuid(), "Doctor");
        var deleteRequest = AuthorizedRequest(HttpMethod.Delete, $"/api/doctors/{doctorId}/photo", otherToken);
        var response = await _client.SendAsync(deleteRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private record DoctorProfileResponse(Guid Id, bool HasPhoto);
}
