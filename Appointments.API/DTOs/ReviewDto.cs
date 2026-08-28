namespace Appointments.DTOs;

public class ReviewDto
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid DoctorId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    /// <summary>Only populated by admin-facing, cross-doctor listings — a single doctor's own
    /// review list already has that context from the page it's shown on.</summary>
    public string? DoctorName { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
