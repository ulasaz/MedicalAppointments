using System.Security.Claims;
using Appointments.Database;
using Appointments.Models;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace Appointments.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly DatabaseContext _dbContext;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IMultiTenantContextSetter _tenantContextSetter;

    public PaymentsController(
        DatabaseContext dbContext,
        IConfiguration config,
        ILogger<PaymentsController> logger,
        IMultiTenantContextSetter tenantContextSetter)
    {
        _dbContext = dbContext;
        _config = config;
        _logger = logger;
        _tenantContextSetter = tenantContextSetter;
    }

    public record CreateIntentRequest(Guid AppointmentId);

    [HttpPost("create-intent")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> CreateIntent([FromBody] CreateIntentRequest req)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(a => a.Id == req.AppointmentId);
        if (appointment == null)
        {
            return NotFound(new { message = "Appointment not found." });
        }

        if (appointment.PatientId != userId)
        {
            return Forbid();
        }

        if (appointment.VisitType != VisitType.Online)
        {
            return BadRequest(new { message = "Only online visits can be paid for online." });
        }

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Rejected)
        {
            return Conflict(new { message = "This appointment can no longer be paid for." });
        }

        if (appointment.IsPaid)
        {
            return Conflict(new { message = "This appointment has already been paid for." });
        }

        // Amount always comes from the stored appointment price, never from the client.
        var options = new PaymentIntentCreateOptions
        {
            Amount = appointment.PriceCents,
            Currency = "pln",
            Metadata = new Dictionary<string, string> { { "appointmentId", appointment.Id.ToString() } }
        };

        var intent = await new PaymentIntentService().CreateAsync(options);

        return Ok(new { clientSecret = intent.ClientSecret });
    }

    public record ConfirmPaymentRequest(string PaymentIntentId);

    /// <summary>
    /// Called by the client right after Stripe redirects back from checkout, so paid status
    /// is reflected immediately instead of waiting on the webhook (which may be delayed or,
    /// in local/dev setups without a Stripe CLI listener, never arrive at all).
    /// </summary>
    [HttpPost("confirm")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest req)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        PaymentIntent intent;
        try
        {
            intent = await new PaymentIntentService().GetAsync(req.PaymentIntentId);
        }
        catch (StripeException)
        {
            return NotFound(new { message = "Payment not found." });
        }

        if (!Guid.TryParse(intent.Metadata.GetValueOrDefault("appointmentId"), out var appointmentId))
        {
            return BadRequest(new { message = "Invalid payment." });
        }

        var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appointment == null)
        {
            return NotFound(new { message = "Appointment not found." });
        }

        if (appointment.PatientId != userId)
        {
            return Forbid();
        }

        // Trust Stripe's own record of the intent's status, never a client-supplied flag.
        if (intent.Status == "succeeded")
        {
            await MarkAppointmentPaidAsync(appointment);
        }

        return Ok(new { isPaid = appointment.IsPaid });
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();
        var webhookSecret = _config["Stripe:WebhookSecret"];

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return BadRequest();
        }

        if (stripeEvent.Type == EventTypes.PaymentIntentSucceeded
            && stripeEvent.Data.Object is PaymentIntent intent
            && Guid.TryParse(intent.Metadata.GetValueOrDefault("appointmentId"), out var appointmentId))
        {
            // Stripe calls this endpoint with no auth and no notion of our tenants, so there's
            // no ambient tenant context for Finbuckle's query filter to use — look the row up
            // across all tenants, then tell the context which one it belongs to before saving
            // so the write goes through as that tenant's data (not as untenanted/orphaned).
            var appointment = await _dbContext.Appointments
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == appointmentId);
            if (appointment != null)
            {
                var tenantId = _dbContext.Entry(appointment).Property<string>("TenantId").CurrentValue;
                _tenantContextSetter.MultiTenantContext = new MultiTenantContext<TenantInfo>(
                    new TenantInfo { Id = tenantId, Identifier = tenantId });
                await MarkAppointmentPaidAsync(appointment);
            }
        }

        return Ok();
    }

    private async Task MarkAppointmentPaidAsync(Appointment appointment)
    {
        if (appointment.IsPaid)
        {
            return;
        }

        appointment.IsPaid = true;
        await _dbContext.SaveChangesAsync();
    }
}
