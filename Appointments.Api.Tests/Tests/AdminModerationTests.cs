using Appointments.Api.Tests.Infrastructure;
using Appointments.DTOs;
using Appointments.Models;
using Appointments.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Shared;

namespace Appointments.Api.Tests.Tests;

public class AdminModerationTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DoctorId = Guid.NewGuid();
    private static readonly Guid PatientId = Guid.NewGuid();

    private static AppointmentService CreateService(Appointments.Database.DatabaseContext dbContext)
    {
        var doctorsClient = new FakeDoctorsClient { Doctor = new DoctorInfoDto { Id = DoctorId, IsActive = true, FullName = "Dr. Jane Doe" } };
        var identityClient = new FakeIdentityClient();
        var clock = new FakeClock { UtcNow = Now };
        var publishEndpoint = new FakePublishEndpoint();
        var config = new ConfigurationBuilder().Build();

        return new AppointmentService(dbContext, doctorsClient, identityClient, clock, config, publishEndpoint);
    }

    private static Appointment MakeAppointment(AppointmentStatus status) => new()
    {
        Id = Guid.NewGuid(),
        PatientId = PatientId,
        DoctorId = DoctorId,
        ClinicId = Guid.NewGuid(),
        StartTime = Now.AddDays(-1),
        EndTime = Now.AddDays(-1).AddMinutes(30),
        Status = status,
        CreatedAt = Now.AddDays(-2)
    };

    // Platform-wide stats

    [Fact]
    public async Task GetAdminStats_NoData_ReturnsAllZeros()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var result = await service.GetAdminStatsAsync();

        result.TotalAppointments.Should().Be(0);
        result.TotalReviews.Should().Be(0);
        result.AverageRating.Should().Be(0);
    }

    [Fact]
    public async Task GetAdminStats_MixedStatuses_CountsEachBucketCorrectly()
    {
        using var dbContext = TestDbContextFactory.Create();
        dbContext.Appointments.Add(MakeAppointment(AppointmentStatus.Pending));
        dbContext.Appointments.Add(MakeAppointment(AppointmentStatus.Confirmed));
        dbContext.Appointments.Add(MakeAppointment(AppointmentStatus.Confirmed));
        dbContext.Appointments.Add(MakeAppointment(AppointmentStatus.Completed));
        dbContext.Appointments.Add(MakeAppointment(AppointmentStatus.Cancelled));
        dbContext.Appointments.Add(MakeAppointment(AppointmentStatus.Rejected));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetAdminStatsAsync();

        result.TotalAppointments.Should().Be(6);
        result.PendingCount.Should().Be(1);
        result.ConfirmedCount.Should().Be(2);
        result.CompletedCount.Should().Be(1);
        result.CancelledCount.Should().Be(1);
        result.RejectedCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAdminStats_WithReviews_ComputesPlatformWideAverage()
    {
        using var dbContext = TestDbContextFactory.Create();
        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = DoctorId, PatientId = PatientId, Rating = 5, CreatedAt = Now });
        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = Guid.NewGuid(), PatientId = PatientId, Rating = 3, CreatedAt = Now });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.GetAdminStatsAsync();

        result.TotalReviews.Should().Be(2);
        result.AverageRating.Should().Be(4);
    }

    // Cross-doctor review listing

    [Fact]
    public async Task GetAllReviews_ReturnsReviewsAcrossAllDoctors_NewestFirst_WithNamesResolved()
    {
        using var dbContext = TestDbContextFactory.Create();
        var otherDoctorId = Guid.NewGuid();
        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = DoctorId, PatientId = PatientId, Rating = 4, Comment = "Older", CreatedAt = Now.AddDays(-1) });
        dbContext.Reviews.Add(new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = otherDoctorId, PatientId = PatientId, Rating = 2, Comment = "Newer", CreatedAt = Now });
        await dbContext.SaveChangesAsync();

        var doctorsClient = new FakeDoctorsClient();
        doctorsClient.Doctors[DoctorId] = new DoctorInfoDto { Id = DoctorId, IsActive = true, FullName = "Dr. Jane Doe" };
        doctorsClient.Doctors[otherDoctorId] = new DoctorInfoDto { Id = otherDoctorId, IsActive = true, FullName = "Dr. John Smith" };
        var identityClient = new FakeIdentityClient();
        var service = new AppointmentService(dbContext, doctorsClient, identityClient, new FakeClock { UtcNow = Now }, new ConfigurationBuilder().Build(), new FakePublishEndpoint());

        var result = await service.GetAllReviewsAsync();

        result.Should().HaveCount(2);
        result[0].Comment.Should().Be("Newer");
        result[0].DoctorName.Should().Be("Dr. John Smith");
        result[1].Comment.Should().Be("Older");
        result[1].DoctorName.Should().Be("Dr. Jane Doe");
    }

    [Fact]
    public async Task GetAllReviews_NoReviews_ReturnsEmptyList()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var result = await service.GetAllReviewsAsync();

        result.Should().BeEmpty();
    }

    // Review deletion (moderation)

    [Fact]
    public async Task DeleteReview_ExistingReview_RemovesIt()
    {
        using var dbContext = TestDbContextFactory.Create();
        var review = new Review { Id = Guid.NewGuid(), AppointmentId = Guid.NewGuid(), DoctorId = DoctorId, PatientId = PatientId, Rating = 1, Comment = "Inappropriate", CreatedAt = Now };
        dbContext.Reviews.Add(review);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await service.DeleteReviewAsync(review.Id);

        var remaining = await service.GetAllReviewsAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteReview_NotFound_ThrowsKeyNotFoundException()
    {
        using var dbContext = TestDbContextFactory.Create();
        var service = CreateService(dbContext);

        var act = () => service.DeleteReviewAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
