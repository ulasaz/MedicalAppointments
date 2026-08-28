using Appointments.Models;

namespace Appointments.DTOs;

public class SlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    /// <summary>Only populated for working-window entries; null for booked ranges.</summary>
    public List<VisitType>? AllowedVisitTypes { get; set; }
}