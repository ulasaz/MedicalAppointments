namespace Appointments.DTOs;

public class AdminStatsDto
{
    public int TotalAppointments { get; set; }
    public int PendingCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public int RejectedCount { get; set; }
    public int TotalReviews { get; set; }
    public double AverageRating { get; set; }
}
