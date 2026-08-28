namespace Shared;

public record AppointmentCancelled(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset CancelledAt
);
