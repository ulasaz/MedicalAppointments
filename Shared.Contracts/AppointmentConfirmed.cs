namespace Shared;

public record AppointmentConfirmed(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset ConfirmedAt
);
