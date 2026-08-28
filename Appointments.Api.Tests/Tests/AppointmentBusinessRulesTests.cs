using Appointments.Api.Tests.Infrastructure;
using Appointments.DTOs;
using Appointments.Interfaces;
using Appointments.Models;
using Appointments.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Shared;

namespace Appointments.Api.Tests.Tests;

public class AppointmentBusinessRulesTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ActiveDoctorId = Guid.NewGuid();

    private static AppointmentService CreateService(
        Appointments.Database.DatabaseContext dbContext,
        IDoctorsClient? doctorsClient = null,
        IIdentityClient? identityClient = null,
        IClock? clock = null,
        FakePublishEndpoint? publishEndpoint = null)
    {
        doctorsClient ??= new FakeDoctorsClient { Doctor = new DoctorInfoDto { Id = ActiveDoctorId, IsActive = true, PriceStationaryCents = 10000 } };
        identityClient ??= new FakeIdentityClient();
        clock ??= new FakeClock { UtcNow = Now };
        publishEndpoint ??= new FakePublishEndpoint();
        var config = new ConfigurationBuilder().Build();

        return new AppointmentService(dbContext, doctorsClient, identityClient, clock, config, publishEndpoint);
    }

    private static AppointmentCreateDto ValidRequest(TimeSpan leadTime, int durationMinutes = 30) => new()
    {
        DoctorId = ActiveDoctorId,
        ClinicId = Guid.NewGuid(),
        StartTime = Now + leadTime,
        DurationMinutes = durationMinutes,
        VisitType = VisitType.Stationary
    };

    // Rule 1: duration bounds (15-120 minutes default)

    [Fact]
    public async Task Book_DurationTooShort_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = ValidRequest(TimeSpan.FromMinutes(60), durationMinutes: 10);

        var act = () => service.BookAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Book_DurationTooLong_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = ValidRequest(TimeSpan.FromMinutes(60), durationMinutes: 130);

        var act = () => service.BookAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Book_DurationWithinBounds_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = ValidRequest(TimeSpan.FromMinutes(60), durationMinutes: 30);

        var result = await service.BookAsync(Guid.NewGuid(), request);

        result.Should().NotBeNull();
    }

    // Rule 2: lead time (at least 30 minutes from "now", via IClock)

    [Fact]
    public async Task Book_StartTimeLessThanLeadTime_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = ValidRequest(TimeSpan.FromMinutes(10));

        var act = () => service.BookAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Book_StartTimeExactlyAtLeadTimeBoundary_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        // Exactly 30 minutes from "now" — "at least" means this boundary is allowed, not rejected.
        var request = ValidRequest(TimeSpan.FromMinutes(30));

        var result = await service.BookAsync(Guid.NewGuid(), request);

        result.Should().NotBeNull();
    }

    // Rule 3: doctor must be active

    [Fact]
    public async Task Book_DoctorNotActive_ThrowsInvalidOperationException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var doctorsClient = new FakeDoctorsClient { Doctor = new DoctorInfoDto { Id = ActiveDoctorId, IsActive = false } };
        var service = CreateService(dbContext, doctorsClient);

        var request = ValidRequest(TimeSpan.FromMinutes(60));

        var act = () => service.BookAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Book_DoctorNotFound_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var doctorsClient = new FakeDoctorsClient { Doctor = null };
        var service = CreateService(dbContext, doctorsClient);

        var request = ValidRequest(TimeSpan.FromMinutes(60));

        var act = () => service.BookAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Rule: a visit type is only bookable if the doctor has set a price for it

    [Fact]
    public async Task Book_VisitTypeNotOfferedByDoctor_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var doctorsClient = new FakeDoctorsClient
        {
            // Only offers Stationary — no PriceOnlineCents set.
            Doctor = new DoctorInfoDto { Id = ActiveDoctorId, IsActive = true, PriceStationaryCents = 10000 }
        };
        var service = CreateService(dbContext, doctorsClient);

        var request = ValidRequest(TimeSpan.FromMinutes(60));
        request.VisitType = VisitType.Online;

        var act = () => service.BookAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Book_VisitTypeOfferedByDoctor_SnapshotsPriceOntoAppointment()
    {
        using var dbContext = TestDbContextFactory.Create();
        var doctorsClient = new FakeDoctorsClient
        {
            Doctor = new DoctorInfoDto { Id = ActiveDoctorId, IsActive = true, PriceOnlineCents = 8000 }
        };
        var service = CreateService(dbContext, doctorsClient);

        var request = ValidRequest(TimeSpan.FromMinutes(60));
        request.VisitType = VisitType.Online;

        var result = await service.BookAsync(Guid.NewGuid(), request);

        result.VisitType.Should().Be(VisitType.Online);
        result.PriceCents.Should().Be(8000);
    }

    // Rule: a schedule window can restrict which visit types are bookable during it

    [Fact]
    public async Task Book_VisitTypeNotAllowedByWindow_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var request = ValidRequest(TimeSpan.FromMinutes(60)); // VisitType.Stationary
        dbContext.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = ActiveDoctorId,
            Date = DateOnly.FromDateTime(request.StartTime),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17),
            AllowedVisitTypes = new List<VisitType> { VisitType.Online } // online-only window
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.BookAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Book_VisitTypeAllowedByWindow_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var request = ValidRequest(TimeSpan.FromMinutes(60)); // VisitType.Stationary
        dbContext.AvailabilitySlots.Add(new AvailabilitySlot
        {
            Id = Guid.NewGuid(),
            DoctorId = ActiveDoctorId,
            Date = DateOnly.FromDateTime(request.StartTime),
            StartTime = TimeSpan.FromHours(9),
            EndTime = TimeSpan.FromHours(17),
            AllowedVisitTypes = new List<VisitType> { VisitType.Stationary, VisitType.Online }
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.BookAsync(Guid.NewGuid(), request);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Book_NoScheduleWindowDefined_DoesNotRestrictVisitType()
    {
        // No AvailabilitySlot rows at all for this doctor/date — schedule windows are only used
        // for restricting visit types when defined, not for requiring a window to exist.
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var request = ValidRequest(TimeSpan.FromMinutes(60));

        var result = await service.BookAsync(Guid.NewGuid(), request);

        result.Should().NotBeNull();
    }

    // Rule 4: no overlap, boundary touching allowed

    [Fact]
    public async Task Book_OverlappingExistingAppointment_ThrowsInvalidOperationException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var existingStart = Now.AddHours(2);
        dbContext.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = ActiveDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = existingStart,
            EndTime = existingStart.AddMinutes(30),
            Status = AppointmentStatus.Pending,
            CreatedAt = Now
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        // Overlaps the existing 2h00-2h30 appointment (starts 15 min into it).
        var request = new AppointmentCreateDto
        {
            DoctorId = ActiveDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = existingStart.AddMinutes(15),
            DurationMinutes = 30
        };

        var act = () => service.BookAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Book_TouchingBoundaryOfExistingAppointment_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var existingStart = Now.AddHours(2);
        var existingEnd = existingStart.AddMinutes(30);
        dbContext.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = ActiveDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = existingStart,
            EndTime = existingEnd,
            Status = AppointmentStatus.Pending,
            CreatedAt = Now
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        // Starts exactly when the existing appointment ends — touching boundaries, not an overlap.
        var request = new AppointmentCreateDto
        {
            DoctorId = ActiveDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = existingEnd,
            DurationMinutes = 30
        };

        var result = await service.BookAsync(Guid.NewGuid(), request);

        result.Should().NotBeNull();
    }

    // Rule 5: no double-cancel

    [Fact]
    public async Task Cancel_AlreadyCancelledAppointment_ThrowsInvalidOperationException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = ActiveDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(2),
            EndTime = Now.AddHours(2).AddMinutes(30),
            Status = AppointmentStatus.Cancelled,
            CreatedAt = Now
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.CancelAsync(appointment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancel_PendingAppointment_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = ActiveDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(2),
            EndTime = Now.AddHours(2).AddMinutes(30),
            Status = AppointmentStatus.Pending,
            CreatedAt = Now
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.CancelAsync(appointment.Id);

        result.Should().BeTrue();
    }

    // DoctorName resolution

    [Fact]
    public async Task Book_PopulatesDoctorNameOnResponse()
    {
        using var dbContext = TestDbContextFactory.Create();
        var doctorsClient = new FakeDoctorsClient
        {
            Doctor = new DoctorInfoDto { Id = ActiveDoctorId, IsActive = true, FullName = "Dr. Jane Doe", PriceStationaryCents = 10000 }
        };
        var service = CreateService(dbContext, doctorsClient);

        var request = ValidRequest(TimeSpan.FromMinutes(60));

        var result = await service.BookAsync(Guid.NewGuid(), request);

        result.DoctorName.Should().Be("Dr. Jane Doe");
    }

    [Fact]
    public async Task GetForPatient_PopulatesDoctorNameForEachDistinctDoctor()
    {
        using var dbContext = TestDbContextFactory.Create();
        var patientId = Guid.NewGuid();
        var otherDoctorId = Guid.NewGuid();

        dbContext.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = ActiveDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(2),
            EndTime = Now.AddHours(2).AddMinutes(30),
            Status = AppointmentStatus.Pending,
            CreatedAt = Now
        });
        dbContext.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = otherDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(3),
            EndTime = Now.AddHours(3).AddMinutes(30),
            Status = AppointmentStatus.Pending,
            CreatedAt = Now
        });
        await dbContext.SaveChangesAsync();

        var doctorsClient = new FakeDoctorsClient();
        doctorsClient.Doctors[ActiveDoctorId] = new DoctorInfoDto { Id = ActiveDoctorId, IsActive = true, FullName = "Dr. Jane Doe" };
        doctorsClient.Doctors[otherDoctorId] = new DoctorInfoDto { Id = otherDoctorId, IsActive = true, FullName = "Dr. John Smith" };

        var service = CreateService(dbContext, doctorsClient);

        var result = await service.GetForPatientAsync(patientId);

        result.Should().HaveCount(2);
        result.Single(a => a.DoctorId == ActiveDoctorId).DoctorName.Should().Be("Dr. Jane Doe");
        result.Single(a => a.DoctorId == otherDoctorId).DoctorName.Should().Be("Dr. John Smith");
    }

    // Event publishing

    [Fact]
    public async Task Book_PublishesAppointmentRequested()
    {
        using var dbContext = TestDbContextFactory.Create();
        var publishEndpoint = new FakePublishEndpoint();
        var service = CreateService(dbContext, publishEndpoint: publishEndpoint);

        var request = ValidRequest(TimeSpan.FromMinutes(60));

        await service.BookAsync(Guid.NewGuid(), request);

        publishEndpoint.PublishedMessages.OfType<AppointmentRequested>().Should().ContainSingle();
    }

    [Fact]
    public async Task Confirm_PublishesAppointmentConfirmed()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = ActiveDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(2),
            EndTime = Now.AddHours(2).AddMinutes(30),
            Status = AppointmentStatus.Pending,
            CreatedAt = Now
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var publishEndpoint = new FakePublishEndpoint();
        var service = CreateService(dbContext, publishEndpoint: publishEndpoint);

        await service.ConfirmAsync(appointment.Id);

        publishEndpoint.PublishedMessages.OfType<AppointmentConfirmed>().Should().ContainSingle();
    }

    [Fact]
    public async Task Cancel_PublishesAppointmentCancelled()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = ActiveDoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(2),
            EndTime = Now.AddHours(2).AddMinutes(30),
            Status = AppointmentStatus.Pending,
            CreatedAt = Now
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var publishEndpoint = new FakePublishEndpoint();
        var service = CreateService(dbContext, publishEndpoint: publishEndpoint);

        await service.CancelAsync(appointment.Id);

        publishEndpoint.PublishedMessages.OfType<AppointmentCancelled>().Should().ContainSingle();
    }
}
