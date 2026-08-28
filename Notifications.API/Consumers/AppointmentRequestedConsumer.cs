using MassTransit;
using Notifications.Interfaces;
using Shared;

namespace Notifications.Consumers;

public class AppointmentRequestedConsumer : IConsumer<AppointmentRequested>
{
    private readonly IDoctorsClient _doctorsClient;
    private readonly IIdentityClient _identityClient;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AppointmentRequestedConsumer> _logger;

    public AppointmentRequestedConsumer(
        IDoctorsClient doctorsClient,
        IIdentityClient identityClient,
        IEmailSender emailSender,
        ILogger<AppointmentRequestedConsumer> logger)
    {
        _doctorsClient = doctorsClient;
        _identityClient = identityClient;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AppointmentRequested> context)
    {
        var appointment = context.Message;

        var doctor = await _doctorsClient.GetDoctorAsync(appointment.DoctorId);
        if (doctor == null)
        {
            _logger.LogWarning("Could not resolve doctor {DoctorId} for appointment {AppointmentId}", appointment.DoctorId, appointment.AppointmentId);
            return;
        }

        var doctorUser = await _identityClient.GetUserAsync(doctor.UserId);
        if (doctorUser == null)
        {
            _logger.LogWarning("Could not resolve user account for doctor {DoctorId}", doctor.Id);
            return;
        }

        var patient = await _identityClient.GetUserAsync(appointment.PatientId);
        var patientName = patient?.DisplayName ?? "A patient";
        var when = $"{appointment.StartTime.UtcDateTime:yyyy-MM-dd HH:mm} UTC";

        await _emailSender.SendAsync(
            doctorUser.Email,
            doctorUser.DisplayName,
            "New appointment request",
            $"{patientName} has requested an appointment with you on {when}.\n\nSign in to CuraSlot to confirm or reject it.");
    }
}
