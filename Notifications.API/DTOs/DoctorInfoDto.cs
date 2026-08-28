namespace Notifications.DTOs;

public class DoctorInfoDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
