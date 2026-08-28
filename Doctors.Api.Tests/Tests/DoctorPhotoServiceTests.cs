using Doctors.Api.Tests.Infrastructure;
using Doctors.DTOs;
using Doctors.Services;
using FluentAssertions;

namespace Doctors.Api.Tests.Tests;

public class DoctorPhotoServiceTests
{
    private static DoctorService CreateService(Doctors.Database.DatabaseContext dbContext) => new(dbContext, new FakeAppointmentsClient());

    private static readonly byte[] SamplePhoto = { 1, 2, 3, 4 };

    private static async Task<Doctors.Models.DoctorProfile> CreateDoctorAsync(Doctors.Database.DatabaseContext dbContext, DoctorService service, Guid userId)
    {
        return await service.CreateAsync(userId, new DoctorProfileUpdateDto(
            "Dr. Jane Doe", "Urology", "Wroclaw", null, true, null, null, null));
    }

    [Fact]
    public async Task Upload_ByOwner_Succeeds_AndProfileReportsHasPhoto()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, service, userId);

        await service.UploadPhotoAsync(doctor.Id, userId, SamplePhoto, "image/jpeg");

        var refetched = await service.GetByIdAsync(doctor.Id);
        refetched.HasPhoto.Should().BeTrue();
    }

    [Fact]
    public async Task Upload_ByNonOwner_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var doctor = await CreateDoctorAsync(dbContext, service, Guid.NewGuid());

        var act = () => service.UploadPhotoAsync(doctor.Id, Guid.NewGuid(), SamplePhoto, "image/jpeg");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Upload_DoctorNotFound_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.UploadPhotoAsync(Guid.NewGuid(), Guid.NewGuid(), SamplePhoto, "image/jpeg");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    public async Task Upload_DisallowedContentType_ThrowsArgumentException(string contentType)
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, service, userId);

        var act = () => service.UploadPhotoAsync(doctor.Id, userId, SamplePhoto, contentType);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetPhoto_AfterUpload_ReturnsBytesAndContentType()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, service, userId);
        await service.UploadPhotoAsync(doctor.Id, userId, SamplePhoto, "image/png");

        var photo = await service.GetPhotoAsync(doctor.Id);

        photo.Data.Should().BeEquivalentTo(SamplePhoto);
        photo.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task GetPhoto_NoPhotoUploaded_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var doctor = await CreateDoctorAsync(dbContext, service, Guid.NewGuid());

        var act = () => service.GetPhotoAsync(doctor.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Delete_ByOwner_ClearsPhoto()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, service, userId);
        await service.UploadPhotoAsync(doctor.Id, userId, SamplePhoto, "image/jpeg");

        await service.DeletePhotoAsync(doctor.Id, userId);

        var refetched = await service.GetByIdAsync(doctor.Id);
        refetched.HasPhoto.Should().BeFalse();
        var act = () => service.GetPhotoAsync(doctor.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Delete_ByNonOwner_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, service, userId);
        await service.UploadPhotoAsync(doctor.Id, userId, SamplePhoto, "image/jpeg");

        var act = () => service.DeletePhotoAsync(doctor.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Upload_ReplacesExistingPhoto()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, service, userId);
        await service.UploadPhotoAsync(doctor.Id, userId, SamplePhoto, "image/jpeg");

        var replacement = new byte[] { 9, 9, 9 };
        await service.UploadPhotoAsync(doctor.Id, userId, replacement, "image/png");

        var photo = await service.GetPhotoAsync(doctor.Id);
        photo.Data.Should().BeEquivalentTo(replacement);
        photo.ContentType.Should().Be("image/png");
    }
}
