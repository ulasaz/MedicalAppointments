using System.Security.Claims;
using Doctors.DTOs;
using Doctors.Interfaces;
using Doctors.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Doctors.Controllers;

[ApiController]
[Route("api/doctors/{doctorId:guid}/services")]
public class MedicalServicesController : ControllerBase
{
    private readonly IMedicalServiceService _medicalServiceService;

    public MedicalServicesController(IMedicalServiceService medicalServiceService)
    {
        _medicalServiceService = medicalServiceService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<MedicalService>>> GetForDoctorAsync(Guid doctorId)
    {
        var services = await _medicalServiceService.GetForDoctorAsync(doctorId);
        return Ok(services);
    }

    [HttpGet("{serviceId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<MedicalService>> GetByIdAsync(Guid doctorId, Guid serviceId)
    {
        try
        {
            var service = await _medicalServiceService.GetByIdAsync(doctorId, serviceId);
            return Ok(service);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Medical service not found." });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<MedicalService>> CreateAsync(Guid doctorId, [FromBody] MedicalServiceUpsertDto request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var service = await _medicalServiceService.CreateAsync(doctorId, userId, request);
            // ASP.NET Core strips the "Async" suffix from action names by default, so the
            // registered action name is "GetForDoctor", not "GetForDoctorAsync" (nameof(...)
            // would resolve to the latter and fail route generation) — same convention
            // DoctorsController.CreateAsync already follows for "GetById".
            return CreatedAtAction("GetForDoctor", new { doctorId }, service);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Doctor profile not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{serviceId:guid}")]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<MedicalService>> UpdateAsync(Guid doctorId, Guid serviceId, [FromBody] MedicalServiceUpsertDto request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var service = await _medicalServiceService.UpdateAsync(doctorId, serviceId, userId, request);
            return Ok(service);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Medical service not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{serviceId:guid}")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> DeleteAsync(Guid doctorId, Guid serviceId)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _medicalServiceService.DeleteAsync(doctorId, serviceId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Medical service not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdString, out userId);
    }
}
