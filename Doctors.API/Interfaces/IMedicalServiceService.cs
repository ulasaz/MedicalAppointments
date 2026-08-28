using Doctors.DTOs;
using Doctors.Models;

namespace Doctors.Interfaces;

public interface IMedicalServiceService
{
    Task<IEnumerable<MedicalService>> GetForDoctorAsync(Guid doctorProfileId);
    Task<MedicalService> GetByIdAsync(Guid doctorProfileId, Guid serviceId);
    Task<MedicalService> CreateAsync(Guid doctorProfileId, Guid requestingUserId, MedicalServiceUpsertDto request);
    Task<MedicalService> UpdateAsync(Guid doctorProfileId, Guid serviceId, Guid requestingUserId, MedicalServiceUpsertDto request);
    Task DeleteAsync(Guid doctorProfileId, Guid serviceId, Guid requestingUserId);
}
