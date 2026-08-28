using Appointments.Interfaces;

namespace Appointments.Helpers;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
