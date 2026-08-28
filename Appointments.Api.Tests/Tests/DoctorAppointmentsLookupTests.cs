using Appointments.Api.Tests.Infrastructure;
using Appointments.DTOs;
using Appointments.Interfaces;
using Appointments.Models;
using Appointments.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Shared;

namespace Appointments.Api.Tests.Tests;

public class DoctorAppointmentsLookupTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static AppointmentService CreateService(
        Appointments.Database.DatabaseContext dbContext,
        IDoctorsClient doctorsClient)
    {
        var identityClient = new FakeIdentityClient();
        var clock = new FakeClock { UtcNow = Now };
        var publishEndpoint = new FakePublishEndpoint();
        var config = new ConfigurationBuilder().Build();

        return new AppointmentService(dbContext, doctorsClient, identityClient, clock, config, publishEndpoint);
    }

    [Fact]
    public async Task GetForDoctorByUserId_ResolvesDoctorProfileId_NotTheRawUserId()
    {
        var userId = Guid.NewGuid();
        var doctorProfileId = Guid.NewGuid();

        using var dbContext = TestDbContextFactory.Create();
        dbContext.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            DoctorId = doctorProfileId,
            PatientId = Guid.NewGuid(),
            ClinicId = Guid.NewGuid(),
            StartTime = Now.AddDays(1),
            EndTime = Now.AddDays(1).AddMinutes(30),
            Status = AppointmentStatus.Pending,
            CreatedAt = Now
        });
        await dbContext.SaveChangesAsync();

        var doctorsClient = new FakeDoctorsClient
        {
            Doctor = new DoctorInfoDto { Id = doctorProfileId, UserId = userId, IsActive = true }
        };
        var service = CreateService(dbContext, doctorsClient);

        var result = await service.GetForDoctorByUserIdAsync(userId);

        result.Should().HaveCount(1);
        result[0].DoctorId.Should().Be(doctorProfileId);
    }

    [Fact]
    public async Task GetForDoctorByUserId_NoDoctorProfileForUser_ReturnsEmptyList()
    {
        using var dbContext = TestDbContextFactory.Create();
        var doctorsClient = new FakeDoctorsClient { Doctor = null };
        var service = CreateService(dbContext, doctorsClient);

        var result = await service.GetForDoctorByUserIdAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }
}
