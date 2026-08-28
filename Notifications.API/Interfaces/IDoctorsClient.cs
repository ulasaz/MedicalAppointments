using Notifications.DTOs;

namespace Notifications.Interfaces;

public interface IDoctorsClient
{
    Task<DoctorInfoDto?> GetDoctorAsync(Guid doctorId);
}
