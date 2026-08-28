using Doctors.Api.Tests.Infrastructure;
using Doctors.DTOs;
using Doctors.Models;
using Doctors.Services;
using FluentAssertions;

namespace Doctors.Api.Tests.Tests;

public class MedicalServiceServiceTests
{
    private static MedicalServiceService CreateService(Doctors.Database.DatabaseContext dbContext) => new(dbContext);

    private static async Task<Doctors.Models.DoctorProfile> CreateDoctorAsync(Doctors.Database.DatabaseContext dbContext, Guid userId)
    {
        var doctor = new Doctors.Models.DoctorProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = "Dr. Jane Doe",
            Specialization = "Urology",
            City = "Wroclaw",
            IsActive = true,
        };
        dbContext.DoctorProfiles.Add(doctor);
        await dbContext.SaveChangesAsync();
        return doctor;
    }

    private static MedicalServiceUpsertDto ValidRequest(
        string name = "Kwalifikacja do operacji",
        int priceCents = 25000,
        List<VisitType>? allowedVisitTypes = null) => new(
        name, "A description", priceCents, allowedVisitTypes ?? new List<VisitType> { VisitType.Stationary, VisitType.Online });

    // Rule: only the owning doctor can manage their services

    [Fact]
    public async Task Create_ByOwner_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);

        var result = await service.CreateAsync(doctor.Id, userId, ValidRequest());

        result.Name.Should().Be("Kwalifikacja do operacji");
        result.PriceCents.Should().Be(25000);
        result.DoctorProfileId.Should().Be(doctor.Id);
    }

    [Fact]
    public async Task Create_ByNonOwner_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var doctor = await CreateDoctorAsync(dbContext, Guid.NewGuid());
        var service = CreateService(dbContext);

        var act = () => service.CreateAsync(doctor.Id, Guid.NewGuid(), ValidRequest());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Create_DoctorProfileNotFound_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // Rule: at least one visit type must be selected

    [Fact]
    public async Task Create_EmptyAllowedVisitTypes_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);

        var act = () => service.CreateAsync(doctor.Id, userId, ValidRequest(allowedVisitTypes: new List<VisitType>()));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Create_DuplicateVisitTypesInRequest_DedupesOnSave()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);

        var result = await service.CreateAsync(doctor.Id, userId,
            ValidRequest(allowedVisitTypes: new List<VisitType> { VisitType.Online, VisitType.Online }));

        result.AllowedVisitTypes.Should().BeEquivalentTo(new[] { VisitType.Online });
    }

    // Rule: price cannot be negative

    [Fact]
    public async Task Create_NegativePrice_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);

        var act = () => service.CreateAsync(doctor.Id, userId, ValidRequest(priceCents: -100));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Create_ZeroPrice_Succeeds()
    {
        // A free service (e.g. a consultation) is a valid state, not a validation error.
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);

        var result = await service.CreateAsync(doctor.Id, userId, ValidRequest(priceCents: 0));

        result.PriceCents.Should().Be(0);
    }

    // GetForDoctorAsync scoping

    [Fact]
    public async Task GetForDoctor_OnlyReturnsServicesForThatDoctor()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ownerA = Guid.NewGuid();
        var ownerB = Guid.NewGuid();
        var doctorA = await CreateDoctorAsync(dbContext, ownerA);
        var doctorB = await CreateDoctorAsync(dbContext, ownerB);
        var service = CreateService(dbContext);
        await service.CreateAsync(doctorA.Id, ownerA, ValidRequest(name: "Service A"));
        await service.CreateAsync(doctorB.Id, ownerB, ValidRequest(name: "Service B"));

        var result = await service.GetForDoctorAsync(doctorA.Id);

        result.Should().ContainSingle().Which.Name.Should().Be("Service A");
    }

    [Fact]
    public async Task GetForDoctor_NoServices_ReturnsEmptyList()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var result = await service.GetForDoctorAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    // GetByIdAsync — also used by Appointments.API to price a booking, so must scope by doctor too

    [Fact]
    public async Task GetById_ExistingServiceForCorrectDoctor_ReturnsIt()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(doctor.Id, userId, ValidRequest());

        var result = await service.GetByIdAsync(doctor.Id, created.Id);

        result.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetById_ServiceExistsButForDifferentDoctor_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var ownerA = Guid.NewGuid();
        var doctorA = await CreateDoctorAsync(dbContext, ownerA);
        var doctorB = await CreateDoctorAsync(dbContext, Guid.NewGuid());
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(doctorA.Id, ownerA, ValidRequest());

        var act = () => service.GetByIdAsync(doctorB.Id, created.Id);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetById_ServiceNotFound_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // Update

    [Fact]
    public async Task Update_ByOwner_PersistsChanges()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(doctor.Id, userId, ValidRequest(priceCents: 100));

        var updated = await service.UpdateAsync(doctor.Id, created.Id, userId, ValidRequest(name: "Renamed", priceCents: 500));

        updated.Name.Should().Be("Renamed");
        updated.PriceCents.Should().Be(500);
    }

    [Fact]
    public async Task Update_ByNonOwner_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(doctor.Id, userId, ValidRequest());

        var act = () => service.UpdateAsync(doctor.Id, created.Id, Guid.NewGuid(), ValidRequest(name: "Hijacked"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Update_ServiceNotFound_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);

        var act = () => service.UpdateAsync(doctor.Id, Guid.NewGuid(), userId, ValidRequest());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Update_InvalidPrice_ThrowsBeforeTouchingTheService()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(doctor.Id, userId, ValidRequest(name: "Original", priceCents: 100));

        var act = () => service.UpdateAsync(doctor.Id, created.Id, userId, ValidRequest(name: "Should not apply", priceCents: -1));

        await act.Should().ThrowAsync<ArgumentException>();
        var unchanged = await service.GetByIdAsync(doctor.Id, created.Id);
        unchanged.Name.Should().Be("Original");
    }

    // Delete

    [Fact]
    public async Task Delete_ByOwner_RemovesService()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(doctor.Id, userId, ValidRequest());

        await service.DeleteAsync(doctor.Id, created.Id, userId);

        var act = () => service.GetByIdAsync(doctor.Id, created.Id);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Delete_ByNonOwner_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(doctor.Id, userId, ValidRequest());

        var act = () => service.DeleteAsync(doctor.Id, created.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Delete_ServiceNotFound_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var userId = Guid.NewGuid();
        var doctor = await CreateDoctorAsync(dbContext, userId);
        var service = CreateService(dbContext);

        var act = () => service.DeleteAsync(doctor.Id, Guid.NewGuid(), userId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
