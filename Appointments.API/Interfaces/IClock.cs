namespace Appointments.Interfaces;

public interface IClock
{
    DateTime UtcNow { get; }
}
