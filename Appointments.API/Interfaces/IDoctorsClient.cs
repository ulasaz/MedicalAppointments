using Appointments.DTOs;

namespace Appointments.Interfaces;

public interface IDoctorsClient
{
    Task<DoctorInfoDto?> GetDoctorAsync(Guid doctorId);
    Task<DoctorInfoDto?> GetDoctorByUserIdAsync(Guid userId);
    Task<MedicalServiceInfoDto?> GetServiceAsync(Guid doctorId, Guid serviceId);
}
