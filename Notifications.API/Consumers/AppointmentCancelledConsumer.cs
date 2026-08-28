using MassTransit;
using Notifications.Interfaces;
using Shared;

namespace Notifications.Consumers;

public class AppointmentCancelledConsumer : IConsumer<AppointmentCancelled>
{
    private readonly IDoctorsClient _doctorsClient;
    private readonly IIdentityClient _identityClient;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AppointmentCancelledConsumer> _logger;

    public AppointmentCancelledConsumer(
        IDoctorsClient doctorsClient,
        IIdentityClient identityClient,
        IEmailSender emailSender,
        ILogger<AppointmentCancelledConsumer> logger)
    {
        _doctorsClient = doctorsClient;
        _identityClient = identityClient;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AppointmentCancelled> context)
    {
        var appointment = context.Message;
        var when = $"{appointment.StartTime.UtcDateTime:yyyy-MM-dd HH:mm} UTC";

        var patient = await _identityClient.GetUserAsync(appointment.PatientId);
        var doctor = await _doctorsClient.GetDoctorAsync(appointment.DoctorId);
        var doctorUser = doctor != null ? await _identityClient.GetUserAsync(doctor.UserId) : null;

        if (patient == null && doctorUser == null)
        {
            _logger.LogWarning("Could not resolve either party for cancelled appointment {AppointmentId}", appointment.AppointmentId);
            return;
        }

        // Either the patient or the doctor can trigger a cancellation, and the event doesn't say
        // which one — so both sides are notified rather than guessing who already knows.
        if (patient != null)
        {
            var doctorName = doctor?.FullName ?? "your doctor";
            await _emailSender.SendAsync(
                patient.Email,
                patient.DisplayName,
                "Your appointment has been cancelled",
                $"Your appointment with {doctorName} on {when} has been cancelled.");
        }

        if (doctorUser != null)
        {
            var patientName = patient?.DisplayName ?? "The patient";
            await _emailSender.SendAsync(
                doctorUser.Email,
                doctorUser.DisplayName,
                "An appointment has been cancelled",
                $"{patientName}'s appointment with you on {when} has been cancelled.");
        }
    }
}
