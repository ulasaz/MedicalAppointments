using MassTransit;
using Notifications.Interfaces;
using Shared;

namespace Notifications.Consumers;

public class AppointmentConfirmedConsumer : IConsumer<AppointmentConfirmed>
{
    private readonly IDoctorsClient _doctorsClient;
    private readonly IIdentityClient _identityClient;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AppointmentConfirmedConsumer> _logger;

    public AppointmentConfirmedConsumer(
        IDoctorsClient doctorsClient,
        IIdentityClient identityClient,
        IEmailSender emailSender,
        ILogger<AppointmentConfirmedConsumer> logger)
    {
        _doctorsClient = doctorsClient;
        _identityClient = identityClient;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AppointmentConfirmed> context)
    {
        var appointment = context.Message;

        var patient = await _identityClient.GetUserAsync(appointment.PatientId);
        if (patient == null)
        {
            _logger.LogWarning("Could not resolve patient {PatientId} for appointment {AppointmentId}", appointment.PatientId, appointment.AppointmentId);
            return;
        }

        var doctor = await _doctorsClient.GetDoctorAsync(appointment.DoctorId);
        var doctorName = doctor?.FullName ?? "your doctor";
        var when = $"{appointment.StartTime.UtcDateTime:yyyy-MM-dd HH:mm} UTC";

        await _emailSender.SendAsync(
            patient.Email,
            patient.DisplayName,
            "Your appointment has been confirmed",
            $"Your appointment with {doctorName} on {when} has been confirmed.");
    }
}
