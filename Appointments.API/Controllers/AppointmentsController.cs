using System.Security.Claims;
using Appointments.DTOs;
using Appointments.Interfaces;
using Appointments.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Appointments.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }
  
    [HttpPost]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<Appointment>> BookAsync([FromBody] AppointmentCreateDto request)
    {
        try
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var response = await _appointmentService.BookAsync(userId, request);

            return Created($"/api/appointments/{response.Id}", response);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Invalid appointment data." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "This appointment cannot be booked." });
        }
    }
    
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<AppointmentInfoDto>> GetByIdAsync(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _appointmentService.GetByIdAsync(id, userId);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Appointment not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("patient")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<IEnumerable<Appointment>>> GetPatientAppointmentsAsync()
    {
        try
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var response = await _appointmentService.GetForPatientAsync(userId);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "No appointments found." });
        }
    }
    
    [HttpGet("doctor")]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<IEnumerable<Appointment>>> GetDoctorAppointmentsAsync()
    {
        try
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized();
            }

            var response = await _appointmentService.GetForDoctorByUserIdAsync(userId);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "No appointments found." });
        }
    }

    [HttpPost("{id}/confirm")]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<bool>> ConfirmAsync(Guid id) 
    {
        try
        {
            var response = await _appointmentService.ConfirmAsync(id);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Appointment not found." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "Appointment cannot be confirmed." });
        }
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<bool>> RejectAsync(Guid id)
    {
        try
        {
            var response = await _appointmentService.RejectAsync(id);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Appointment not found." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "Appointment cannot be rejected." }); 
        }
    }

    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<bool>> CancelAsync(Guid id)
    {
        try
        {
            var response = await _appointmentService.CancelAsync(id);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Appointment not found." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "Appointment cannot be cancelled." }); 
        }
    }
    
    [HttpPost("{id}/complete")]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<bool>> CompleteAsync(Guid id)
    {
        try
        {
            var response = await _appointmentService.CompleteAsync(id);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Appointment not found." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "Appointment cannot be completed." });
        }
    }

    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ReviewDto>> AddReviewAsync(Guid id, [FromBody] ReviewCreateDto request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _appointmentService.AddReviewAsync(userId, id, request);
            return Created($"/api/appointments/{id}/review", response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Appointment not found." });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Rating must be between 1 and 5." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "This appointment cannot be reviewed." });
        }
    }

    [HttpGet("/api/doctors/{id:guid}/rating")]
    [AllowAnonymous]
    public async Task<ActionResult<DoctorRatingDto>> GetDoctorRatingAsync(Guid id)
    {
        var response = await _appointmentService.GetDoctorRatingAsync(id);
        return Ok(response);
    }

    [HttpGet("/api/doctors/ratings")]
    [AllowAnonymous]
    public async Task<ActionResult<List<DoctorRatingDto>>> GetDoctorRatingsAsync([FromQuery] List<Guid> ids)
    {
        var response = await _appointmentService.GetDoctorRatingsAsync(ids ?? new List<Guid>());
        return Ok(response);
    }

    [HttpGet("/api/doctors/{id:guid}/reviews")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ReviewDto>>> GetDoctorReviewsAsync(Guid id)
    {
        var response = await _appointmentService.GetDoctorReviewsAsync(id);
        return Ok(response);
    }

    [HttpGet("/api/doctors/{id:guid}/available-slots")]
    [AllowAnonymous]
    public async Task<ActionResult<DayAvailabilityDto>> GetAvailableSlotsAsync(Guid id, [FromQuery] DateOnly date)
    {
        try
        {
            var response = await _appointmentService.GetAvailableSlotsAsync(id, date);
            return Ok(response);
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Doctor not found." });
        }
    }

    [HttpGet("/api/doctors/{id:guid}/schedule")]
    [AllowAnonymous]
    public async Task<ActionResult<List<AvailabilitySlotDto>>> GetScheduleAsync(Guid id)
    {
        var response = await _appointmentService.GetScheduleAsync(id);
        return Ok(response);
    }

    [HttpPost("/api/doctors/{id:guid}/schedule")]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<AvailabilitySlotDto>> AddScheduleSlotAsync(Guid id, [FromBody] AvailabilitySlotCreateDto request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _appointmentService.AddScheduleSlotAsync(userId, id, request);
            return Created($"/api/doctors/{id}/schedule/{response.Id}", response);
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = "Invalid schedule window." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "This window overlaps with an existing schedule window." });
        }
    }

    [HttpDelete("/api/doctors/{id:guid}/schedule/{slotId:guid}")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> DeleteScheduleSlotAsync(Guid id, Guid slotId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _appointmentService.DeleteScheduleSlotAsync(userId, id, slotId);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "Schedule window not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminStatsDto>> GetAdminStatsAsync()
    {
        var response = await _appointmentService.GetAdminStatsAsync();
        return Ok(response);
    }

    [HttpGet("admin/reviews")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<ReviewDto>>> GetAllReviewsAsync()
    {
        var response = await _appointmentService.GetAllReviewsAsync();
        return Ok(response);
    }

    [HttpDelete("admin/reviews/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteReviewAsync(Guid id)
    {
        try
        {
            await _appointmentService.DeleteReviewAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Review not found." });
        }
    }
}