using Doctors.Api.Tests.Infrastructure;
using Doctors.DTOs;
using Doctors.Models;
using Doctors.Services;
using FluentAssertions;

namespace Doctors.Api.Tests.Tests;

public class DoctorServiceTests
{
    private static DoctorService CreateService(Doctors.Database.DatabaseContext dbContext, FakeAppointmentsClient? appointmentsClient = null)
    {
        appointmentsClient ??= new FakeAppointmentsClient();
        return new DoctorService(dbContext, appointmentsClient);
    }

    private static DoctorProfileUpdateDto ValidProfile(
        string fullName = "Dr. Jane Doe",
        string specialization = "Urology",
        string city = "Wroclaw",
        bool isActive = true,
        List<string>? conditionsTreated = null) => new(
        fullName, specialization, city, "A description", isActive, 10000, 8000, conditionsTreated);

    // Rule: a user can only have one doctor profile

    [Fact]
    public async Task Create_NewUser_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();

        var result = await service.CreateAsync(userId, ValidProfile());

        result.UserId.Should().Be(userId);
        result.FullName.Should().Be("Dr. Jane Doe");
    }

    [Fact]
    public async Task Create_UserAlreadyHasProfile_ThrowsInvalidOperationException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        await service.CreateAsync(userId, ValidProfile());

        var act = () => service.CreateAsync(userId, ValidProfile());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_NoConditionsTreated_DefaultsToEmptyList()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var result = await service.CreateAsync(Guid.NewGuid(), ValidProfile(conditionsTreated: null));

        result.ConditionsTreated.Should().NotBeNull();
        result.ConditionsTreated.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WithConditionsTreated_PersistsThem()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var result = await service.CreateAsync(Guid.NewGuid(), ValidProfile(conditionsTreated: new List<string> { "Ból biodra", "Kolana szpotawe" }));

        result.ConditionsTreated.Should().BeEquivalentTo(new[] { "Ból biodra", "Kolana szpotawe" });
    }

    // GetById / GetByUserId lookups

    [Fact]
    public async Task GetById_ExistingProfile_ReturnsIt()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(Guid.NewGuid(), ValidProfile());

        var result = await service.GetByIdAsync(created.Id);

        result.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetById_MissingProfile_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.GetByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetById_ReviewCountZero_DoesNotOverwriteRatingWithZeroAverage()
    {
        // A doctor with zero reviews shouldn't display a misleading "0.0 average" — the
        // rating fields should stay unset (null) rather than snap to zero.
        using var dbContext = TestDbContextFactory.Create();
        var appointmentsClient = new FakeAppointmentsClient();
        var service = CreateService(dbContext, appointmentsClient);
        var created = await service.CreateAsync(Guid.NewGuid(), ValidProfile());
        appointmentsClient.Ratings[created.Id] = new DoctorRatingDto { DoctorId = created.Id, AverageRating = 0, ReviewCount = 0 };

        var result = await service.GetByIdAsync(created.Id);

        result.AverageRating.Should().BeNull();
    }

    [Fact]
    public async Task GetById_WithReviews_PopulatesAverageRatingAndCount()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointmentsClient = new FakeAppointmentsClient();
        var service = CreateService(dbContext, appointmentsClient);
        var created = await service.CreateAsync(Guid.NewGuid(), ValidProfile());
        appointmentsClient.Ratings[created.Id] = new DoctorRatingDto { DoctorId = created.Id, AverageRating = 4.5, ReviewCount = 2 };

        var result = await service.GetByIdAsync(created.Id);

        result.AverageRating.Should().Be(4.5);
        result.ReviewCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByUserId_ExistingProfile_ReturnsIt()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var created = await service.CreateAsync(userId, ValidProfile());

        var result = await service.GetByUserIdAsync(userId);

        result.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetByUserId_NoProfileForUser_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.GetByUserIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // Rule: only the owning doctor can edit their profile

    [Fact]
    public async Task Update_ByOwner_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var created = await service.CreateAsync(userId, ValidProfile());

        var updated = await service.UpdateAsync(created.Id, userId, ValidProfile(fullName: "Dr. Jane Updated"));

        updated.FullName.Should().Be("Dr. Jane Updated");
    }

    [Fact]
    public async Task Update_ByNonOwner_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(Guid.NewGuid(), ValidProfile());

        var act = () => service.UpdateAsync(created.Id, Guid.NewGuid(), ValidProfile(fullName: "Hijacked"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Update_ProfileNotFound_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), ValidProfile());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Update_ReplacesConditionsTreated_NotMerges()
    {
        // The endpoint is a full replace, not a patch — sending a shorter list should
        // actually shrink what's stored, not merge with the previous list.
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var created = await service.CreateAsync(userId, ValidProfile(conditionsTreated: new List<string> { "A", "B", "C" }));

        var updated = await service.UpdateAsync(created.Id, userId, ValidProfile(conditionsTreated: new List<string> { "A" }));

        updated.ConditionsTreated.Should().BeEquivalentTo(new[] { "A" });
    }

    [Fact]
    public async Task Update_OmittedConditionsTreated_ClearsToEmptyList()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);
        var userId = Guid.NewGuid();
        var created = await service.CreateAsync(userId, ValidProfile(conditionsTreated: new List<string> { "A", "B" }));

        var updated = await service.UpdateAsync(created.Id, userId, ValidProfile(conditionsTreated: null));

        updated.ConditionsTreated.Should().BeEmpty();
    }
}
