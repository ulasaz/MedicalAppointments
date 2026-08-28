using Appointments.Interfaces;

namespace Appointments.Api.Tests.Infrastructure;

public class FakeClock : IClock
{
    public DateTime UtcNow { get; set; }
}
