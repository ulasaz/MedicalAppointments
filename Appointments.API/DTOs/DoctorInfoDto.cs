namespace Appointments.DTOs;

public class DoctorInfoDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int? PriceStationaryCents { get; set; }
    public int? PriceOnlineCents { get; set; }
}
