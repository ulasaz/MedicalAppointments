using Doctors.DTOs;
using Doctors.Models;

namespace Doctors.Interfaces;

public interface IDoctorService
{
    Task<IEnumerable<DoctorProfile>> SearchAsync(string? specialization, string? city, string? name);
    Task<DoctorProfile> GetByIdAsync(Guid id);
    Task<DoctorProfile> GetByUserIdAsync(Guid userId);
    Task<DoctorProfile> CreateAsync(Guid userId, DoctorProfileUpdateDto profile);
    Task<DoctorProfile> UpdateAsync(Guid id, Guid requestingUserId, DoctorProfileUpdateDto profile);
    Task<DoctorPhoto> GetPhotoAsync(Guid id);
    Task UploadPhotoAsync(Guid id, Guid requestingUserId, byte[] data, string contentType);
    Task DeletePhotoAsync(Guid id, Guid requestingUserId);
    Task<IEnumerable<DoctorProfile>> GetAllForAdminAsync();
    Task<DoctorProfile> SetActiveStatusAsync(Guid id, bool isActive);
}
