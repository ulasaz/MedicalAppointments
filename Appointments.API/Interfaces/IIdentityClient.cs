using Appointments.DTOs;

namespace Appointments.Interfaces;

public interface IIdentityClient
{
    Task<PatientInfoDto?> GetUserAsync(Guid userId);
}
