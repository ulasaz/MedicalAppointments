using Appointments.DTOs;
using Appointments.Interfaces;

namespace Appointments.Api.Tests.Infrastructure;

public class FakeIdentityClient : IIdentityClient
{
    public PatientInfoDto? Patient { get; set; }

    public Task<PatientInfoDto?> GetUserAsync(Guid userId) => Task.FromResult(Patient);
}
