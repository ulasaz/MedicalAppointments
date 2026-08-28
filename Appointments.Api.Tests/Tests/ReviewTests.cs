using Appointments.Api.Tests.Infrastructure;
using Appointments.DTOs;
using Appointments.Interfaces;
using Appointments.Models;
using Appointments.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Shared;

namespace Appointments.Api.Tests.Tests;

public class ReviewTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly Guid PatientId = Guid.NewGuid();

    private static AppointmentService CreateService(Appointments.Database.DatabaseContext dbContext)
    {
        var doctorsClient = new FakeDoctorsClient { Doctor = new DoctorInfoDto { Id = DoctorId, IsActive = true } };
        var identityClient = new FakeIdentityClient();
        var clock = new FakeClock { UtcNow = Now };
        var publishEndpoint = new FakePublishEndpoint();
        var config = new ConfigurationBuilder().Build();

        return new AppointmentService(dbContext, doctorsClient, identityClient, clock, config, publishEndpoint);
    }

    private static Appointment CompletedAppointment() => new()
    {
        Id = Guid.NewGuid(),
        PatientId = PatientId,
        DoctorId = DoctorId,
        ClinicId = Guid.NewGuid(),
        StartTime = Now.AddHours(-3),
        EndTime = Now.AddHours(-2),
        Status = AppointmentStatus.Completed,
        CreatedAt = Now.AddDays(-1)
    };

    // Rule: only Confirmed appointments can be completed — the doctor can end the visit
    // immediately and doesn't have to wait for the scheduled end time to pass.

    [Fact]
    public async Task Complete_ConfirmedAppointmentPastEndTime_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = PatientId,
            DoctorId = DoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(-2),
            EndTime = Now.AddHours(-1),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = Now.AddDays(-1)
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.CompleteAsync(appointment.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Complete_ConfirmedAppointmentBeforeEndTime_Succeeds()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = PatientId,
            DoctorId = DoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(1),
            EndTime = Now.AddHours(2),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = Now.AddDays(-1)
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.CompleteAsync(appointment.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Complete_PendingAppointment_ThrowsInvalidOperationException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = PatientId,
            DoctorId = DoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(-2),
            EndTime = Now.AddHours(-1),
            Status = AppointmentStatus.Pending,
            CreatedAt = Now.AddDays(-1)
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.CompleteAsync(appointment.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Complete_AppointmentNotFound_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.CompleteAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Rule: rating must be 1-5

    [Fact]
    public async Task AddReview_RatingTooLow_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = CompletedAppointment();
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.AddReviewAsync(PatientId, appointment.Id, new ReviewCreateDto { Rating = 0 });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddReview_RatingTooHigh_ThrowsArgumentException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = CompletedAppointment();
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.AddReviewAsync(PatientId, appointment.Id, new ReviewCreateDto { Rating = 6 });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Rule: appointment must exist

    [Fact]
    public async Task AddReview_AppointmentNotFound_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.AddReviewAsync(PatientId, Guid.NewGuid(), new ReviewCreateDto { Rating = 5 });

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // Rule: only the patient who had the appointment can review it

    [Fact]
    public async Task AddReview_RequestingUserIsNotTheAppointmentPatient_ThrowsUnauthorizedAccessException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = CompletedAppointment();
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.AddReviewAsync(Guid.NewGuid(), appointment.Id, new ReviewCreateDto { Rating = 5 });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // Rule: only Completed appointments can be reviewed

    [Fact]
    public async Task AddReview_AppointmentNotCompleted_ThrowsInvalidOperationException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = PatientId,
            DoctorId = DoctorId,
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddHours(-2),
            EndTime = Now.AddHours(-1),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = Now.AddDays(-1)
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.AddReviewAsync(PatientId, appointment.Id, new ReviewCreateDto { Rating = 5 });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // Rule: one review per appointment

    [Fact]
    public async Task AddReview_AppointmentAlreadyReviewed_ThrowsInvalidOperationException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = CompletedAppointment();
        dbContext.Appointments.Add(appointment);
        dbContext.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            DoctorId = DoctorId,
            PatientId = PatientId,
            Rating = 4,
            CreatedAt = Now.AddHours(-1)
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var act = () => service.AddReviewAsync(PatientId, appointment.Id, new ReviewCreateDto { Rating = 5 });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddReview_ValidReview_PersistsAndReturnsExpectedShape()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = CompletedAppointment();
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.AddReviewAsync(PatientId, appointment.Id, new ReviewCreateDto { Rating = 5, Comment = "Great doctor" });

        result.AppointmentId.Should().Be(appointment.Id);
        result.DoctorId.Should().Be(DoctorId);
        result.PatientId.Should().Be(PatientId);
        result.Rating.Should().Be(5);
        result.Comment.Should().Be("Great doctor");

        var stored = await dbContext.Reviews.FindAsync(result.Id);
        stored.Should().NotBeNull();
    }

    // Average rating aggregation

    [Fact]
    public async Task GetDoctorRating_NoReviews_ReturnsZeroAverageAndCount()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var result = await service.GetDoctorRatingAsync(DoctorId);

        result.ReviewCount.Should().Be(0);
        result.AverageRating.Should().Be(0);
    }

    [Fact]
    public async Task GetDoctorRating_WithReviews_ReturnsCorrectAverage()
    {
        using var dbContext = TestDbContextFactory.Create();
        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = DoctorId, PatientId = PatientId, Rating = 5, CreatedAt = Now });
        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = DoctorId, PatientId = Guid.NewGuid(), Rating = 3, CreatedAt = Now });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetDoctorRatingAsync(DoctorId);

        result.ReviewCount.Should().Be(2);
        result.AverageRating.Should().Be(4);
    }

    [Fact]
    public async Task GetDoctorRatings_Batched_ReturnsCorrectPerDoctorAverages()
    {
        using var dbContext = TestDbContextFactory.Create();
        var otherDoctorId = Guid.NewGuid();

        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = DoctorId, PatientId = PatientId, Rating = 5, CreatedAt = Now });
        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = otherDoctorId, PatientId = PatientId, Rating = 2, CreatedAt = Now });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetDoctorRatingsAsync(new List<Guid> { DoctorId, otherDoctorId });

        result.Should().HaveCount(2);
        result.Single(r => r.DoctorId == DoctorId).AverageRating.Should().Be(5);
        result.Single(r => r.DoctorId == otherDoctorId).AverageRating.Should().Be(2);
    }

    // Full review listing (shown on the doctor's public profile)

    [Fact]
    public async Task GetDoctorReviews_ReturnsAllReviewsForDoctor_NewestFirst()
    {
        using var dbContext = TestDbContextFactory.Create();
        var otherDoctorId = Guid.NewGuid();

        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = DoctorId, PatientId = PatientId, Rating = 3, Comment = "Older", CreatedAt = Now.AddDays(-2) });
        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = DoctorId, PatientId = PatientId, Rating = 5, Comment = "Newer", CreatedAt = Now.AddDays(-1) });
        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = otherDoctorId, PatientId = PatientId, Rating = 1, CreatedAt = Now });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetDoctorReviewsAsync(DoctorId);

        result.Should().HaveCount(2);
        result[0].Comment.Should().Be("Newer");
        result[1].Comment.Should().Be("Older");
    }

    [Fact]
    public async Task GetDoctorReviews_NoReviews_ReturnsEmptyList()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var result = await service.GetDoctorReviewsAsync(DoctorId);

        result.Should().BeEmpty();
    }

    // Patient's own appointment history exposes their review content, not just a boolean flag

    [Fact]
    public async Task GetForPatient_AppointmentHasReview_ExposesRatingAndComment()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = CompletedAppointment();
        dbContext.Appointments.Add(appointment);
        dbContext.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(),
            AppointmentId = appointment.Id,
            DoctorId = DoctorId,
            PatientId = PatientId,
            Rating = 4,
            Comment = "Solid visit",
            CreatedAt = Now
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetForPatientAsync(PatientId);

        var reviewed = result.Single(a => a.Id == appointment.Id);
        reviewed.HasReview.Should().BeTrue();
        reviewed.ReviewRating.Should().Be(4);
        reviewed.ReviewComment.Should().Be("Solid visit");
    }

    [Fact]
    public async Task GetForPatient_AppointmentNotReviewed_ReviewFieldsAreNull()
    {
        using var dbContext = TestDbContextFactory.Create();
        var appointment = CompletedAppointment();
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetForPatientAsync(PatientId);

        var notReviewed = result.Single(a => a.Id == appointment.Id);
        notReviewed.HasReview.Should().BeFalse();
        notReviewed.ReviewRating.Should().BeNull();
        notReviewed.ReviewComment.Should().BeNull();
    }
}
