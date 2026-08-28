using Appointments.Api.Tests.Infrastructure;
using Appointments.DTOs;
using Appointments.Interfaces;
using Appointments.Models;
using Appointments.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Shared;

namespace Appointments.Api.Tests.Tests;

public class AvailabilityScheduleTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly TestDate = new(2026, 1, 5);
    private static readonly DateOnly OtherDate = new(2026, 1, 6);
    private static readonly DateOnly PastDate = new(2025, 12, 31);
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly Guid OwningUserId = Guid.NewGuid();

    private static AppointmentService CreateService(
        Appointments.Database.DatabaseContext dbContext,
        IDoctorsClient? doctorsClient = null)
    {
        doctorsClient ??= new FakeDoctorsClient
        {
            Doctor = new DoctorInfoDto { Id = DoctorId, UserId = OwningUserId, IsActive = true }
        };
        var identityClient = new FakeIdentityClient();
        var clock = new FakeClock { UtcNow = Now };
        var publishEndpoint = new FakePublishEndpoint();
        var config = new ConfigurationBuilder().Build();

        return new AppointmentService(dbContext, doctorsClient, identityClient, clock, config, publishEndpoint);
    }

    private static AvailabilitySlotCreateDto Window(DateOnly date, int startHour, int endHour) => new()
    {
        Date = date,
        StartTime = TimeSpan.FromHours(startHour),
        EndTime = TimeSpan.FromHours(endHour)
    };

    // Rule: StartTime must be before EndTime

    [Fact]
    public async Task AddSlot_StartTimeAfterEndTime_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = Window(TestDate, 12, 9);

        var act = () => service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddSlot_StartTimeEqualsEndTime_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = new AvailabilitySlotCreateDto
        {
            Date = TestDate,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(9)
        };

        var act = () => service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Rule: cannot add availability in the past

    [Fact]
    public async Task AddSlot_DateInThePast_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = Window(PastDate, 9, 12);

        var act = () => service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Rule: no overlapping windows on the same date, touching boundaries allowed

    [Fact]
    public async Task AddSlot_OverlapsExistingWindowSameDate_ThrowsInvalidOperationException()
    {
        using var dbContext = TestDbContextFactory.Create();
        dbContext.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = DoctorId,
            Date = TestDate,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(12)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        // Overlaps the existing 9-12 window (starts 1h into it).
        var request = Window(TestDate, 10, 14);

        var act = () => service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddSlot_NewWindowFullyContainsExistingWindowSameDate_ThrowsInvalidOperationException()
    {
        using var dbContext = TestDbContextFactory.Create();
        dbContext.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = DoctorId,
            Date = TestDate,
            StartTime = TimeSpan.FromHours(10),
            EndTime = TimeSpan.FromHours(11)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request = Window(TestDate, 9, 12);

        var act = () => service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddSlot_TouchingBoundaryOfExistingWindowSameDate_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        dbContext.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = DoctorId,
            Date = TestDate,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(12)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        // Starts exactly when the existing window ends — touching boundaries, not an overlap.
        var request = Window(TestDate, 12, 15);

        var result = await service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AddSlot_SameTimeRangeDifferentDate_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        dbContext.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = DoctorId,
            Date = TestDate,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(12)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var request = Window(OtherDate, 9, 12);

        var result = await service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AddSlot_NoExistingWindows_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = Window(TestDate, 9, 12);

        var result = await service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        result.Should().NotBeNull();
        result.DoctorId.Should().Be(DoctorId);
    }

    // Rule: AllowedVisitTypes defaults to both when not specified, or is stored as given

    [Fact]
    public async Task AddSlot_NoAllowedVisitTypesSpecified_DefaultsToBoth()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = Window(TestDate, 9, 12);

        var result = await service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        result.AllowedVisitTypes.Should().BeEquivalentTo(new[] { VisitType.Stationary, VisitType.Online });
    }

    [Fact]
    public async Task AddSlot_AllowedVisitTypesRestrictedToOnline_IsStoredAsGiven()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = Window(TestDate, 9, 12);
        request.AllowedVisitTypes = new List<VisitType> { VisitType.Online };

        var result = await service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        result.AllowedVisitTypes.Should().BeEquivalentTo(new[] { VisitType.Online });
    }

    // Ownership

    [Fact]
    public async Task AddSlot_RequestingUserIsNotOwningDoctor_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = Window(TestDate, 9, 12);

        var act = () => service.AddScheduleSlotAsync(Guid.NewGuid(), DoctorId, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AddSlot_DoctorNotFound_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var doctorsClient = new FakeDoctorsClient { Doctor = null };
        var service = CreateService(dbContext, doctorsClient);

        var request = Window(TestDate, 9, 12);

        var act = () => service.AddScheduleSlotAsync(OwningUserId, DoctorId, request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Delete

    [Fact]
    public async Task DeleteSlot_RequestingUserIsNotOwningDoctor_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = DoctorId,
            Date = TestDate,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(12)
        };
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.DeleteScheduleSlotAsync(Guid.NewGuid(), DoctorId, slot.Id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteSlot_SlotDoesNotExist_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.DeleteScheduleSlotAsync(OwningUserId, DoctorId, Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteSlot_OwningDoctor_RemovesWindow()
    {
        using var dbContext = TestDbContextFactory.Create();
        var slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = DoctorId,
            Date = TestDate,
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(12)
        };
        dbContext.AvailabilitySlots.Add(slot);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await service.DeleteScheduleSlotAsync(OwningUserId, DoctorId, slot.Id);

        var remaining = await service.GetScheduleAsync(DoctorId);
        remaining.Should().BeEmpty();
    }
}
