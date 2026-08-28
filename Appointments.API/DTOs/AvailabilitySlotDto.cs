using Appointments.Models;

namespace Appointments.DTOs;

public class AvailabilitySlotDto
{
    public Guid Id { get; set; }
    public Guid DoctorId { get; set; }
    public DateOnly Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public List<VisitType> AllowedVisitTypes { get; set; } = new();
}
