namespace Appointments.DTOs;

public class DayAvailabilityDto
{
    public List<SlotDto> WorkingWindows { get; set; } = new();
    public List<SlotDto> BookedRanges { get; set; } = new();
}
