using Doctors.DTOs;

namespace Doctors.Interfaces;

public interface IAppointmentsClient
{
    Task<DoctorRatingDto?> GetRatingAsync(Guid doctorId);
    Task<Dictionary<Guid, DoctorRatingDto>> GetRatingsAsync(IEnumerable<Guid> doctorIds);
}
