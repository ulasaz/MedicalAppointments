using Appointments.Models;

namespace Appointments.DTOs;

public class AvailabilitySlotCreateDto
{
    public DateOnly Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    /// <summary>Visit types bookable during this window. Null/empty means both (default).</summary>
    public List<VisitType>? AllowedVisitTypes { get; set; }
}
