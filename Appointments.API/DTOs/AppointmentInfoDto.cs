using Appointments.Models;

namespace Appointments.DTOs;

public class AppointmentInfoDto
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid ClinicId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public bool HasReview { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewComment { get; set; }
    public VisitType VisitType { get; set; }
    public int PriceCents { get; set; }
    public bool IsPaid { get; set; }
    public Guid? MedicalServiceId { get; set; }
    public string? ServiceName { get; set; }
}