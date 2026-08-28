using Finbuckle.MultiTenant.Abstractions;

namespace Appointments.Models;

[MultiTenant]
public class Appointment
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid ClinicId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public AppointmentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public VisitType VisitType { get; set; }
    public int PriceCents { get; set; }
    public bool IsPaid { get; set; }
    public Guid? MedicalServiceId { get; set; }
    public string? ServiceName { get; set; }
}
