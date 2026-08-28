namespace Appointments.DTOs;

public class DoctorRatingDto
{
    public Guid DoctorId { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
