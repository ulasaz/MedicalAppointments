using Appointments.DTOs;

namespace Appointments.Interfaces;

public interface IAppointmentService
{
    Task<AppointmentInfoDto> BookAsync(Guid patientId, AppointmentCreateDto dto);
    Task<AppointmentInfoDto> GetByIdAsync(Guid appointmentId, Guid requestingUserId);
    Task<List<AppointmentInfoDto>> GetForPatientAsync(Guid patientId);
    Task<List<AppointmentInfoDto>> GetForDoctorAsync(Guid doctorId);
    Task<List<AppointmentInfoDto>> GetForDoctorByUserIdAsync(Guid userId);
    Task<bool> CancelAsync(Guid appointmentId);
    Task<bool> ConfirmAsync(Guid appointmentId);
    Task<bool> RejectAsync(Guid appointmentId);
    Task<bool> CompleteAsync(Guid appointmentId);
    Task<DayAvailabilityDto> GetAvailableSlotsAsync(Guid doctorId, DateOnly date);
    Task<List<AvailabilitySlotDto>> GetScheduleAsync(Guid doctorId);
    Task<AvailabilitySlotDto> AddScheduleSlotAsync(Guid requestingUserId, Guid doctorId, AvailabilitySlotCreateDto dto);
    Task DeleteScheduleSlotAsync(Guid requestingUserId, Guid doctorId, Guid slotId);
    Task<ReviewDto> AddReviewAsync(Guid patientId, Guid appointmentId, ReviewCreateDto dto);
    Task<DoctorRatingDto> GetDoctorRatingAsync(Guid doctorId);
    Task<List<DoctorRatingDto>> GetDoctorRatingsAsync(List<Guid> doctorIds);
    Task<List<ReviewDto>> GetDoctorReviewsAsync(Guid doctorId);
    Task<AdminStatsDto> GetAdminStatsAsync();
    Task<List<ReviewDto>> GetAllReviewsAsync();
    Task DeleteReviewAsync(Guid reviewId);
}