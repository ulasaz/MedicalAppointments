using Appointments.Models;

namespace Appointments.DTOs;

public class AppointmentCreateDto
{
    public Guid DoctorId { get; set; }
    public Guid ClinicId { get; set; }
    public DateTime StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public VisitType VisitType { get; set; }
    public Guid? MedicalServiceId { get; set; }
}