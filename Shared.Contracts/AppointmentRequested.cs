namespace Shared;

public record AppointmentRequested(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset RequestedAt
);
