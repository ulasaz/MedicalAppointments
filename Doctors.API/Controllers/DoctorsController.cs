using System.Security.Claims;
using Doctors.DTOs;
using Doctors.Interfaces;
using Doctors.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Doctors.Controllers;

[ApiController]
[Route("api/doctors")]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorService _doctorService;

    public DoctorsController(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<DoctorProfile>>> SearchAsync(
        [FromQuery] string? specialization,
        [FromQuery] string? city,
        [FromQuery] string? name)
    {
        var response = await _doctorService.SearchAsync(specialization, city, name);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<DoctorProfile>> GetByIdAsync(Guid id)
    {
        try
        {
            var response = await _doctorService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Doctor profile not found." });
        }
    }

    [HttpGet("by-user/{userId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<DoctorProfile>> GetByUserIdAsync(Guid userId)
    {
        try
        {
            var response = await _doctorService.GetByUserIdAsync(userId);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Doctor profile not found." });
        }
    }

    [HttpGet("me")]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<DoctorProfile>> GetMineAsync()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _doctorService.GetByUserIdAsync(userId);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Doctor profile not found." });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<DoctorProfile>> CreateAsync([FromBody] DoctorProfileUpdateDto request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _doctorService.CreateAsync(userId, request); 
            return CreatedAtAction("GetById", new { id = response.Id }, response);
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "A doctor profile already exists for this user." });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Doctor")]
    public async Task<ActionResult<DoctorProfile>> UpdateAsync(Guid id, [FromBody] DoctorProfileUpdateDto request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _doctorService.UpdateAsync(id, userId, request);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Doctor profile not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private const long MaxPhotoBytes = 2 * 1024 * 1024;

    [HttpGet("{id:guid}/photo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPhotoAsync(Guid id)
    {
        try
        {
            var photo = await _doctorService.GetPhotoAsync(id);
            return File(photo.Data, photo.ContentType);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("{id:guid}/photo")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> UploadPhotoAsync(Guid id, IFormFile file)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No photo was uploaded." });
        }

        if (file.Length > MaxPhotoBytes)
        {
            return BadRequest(new { message = "Photo must be 2MB or smaller." });
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        try
        {
            await _doctorService.UploadPhotoAsync(id, userId, stream.ToArray(), file.ContentType);
            return NoContent();
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

    [HttpDelete("{id:guid}/photo")]
    [Authorize(Roles = "Doctor")]
    public async Task<IActionResult> DeletePhotoAsync(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await _doctorService.DeletePhotoAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Doctor profile not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    public record SetActiveStatusRequest(bool IsActive);

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<DoctorProfile>>> GetAllForAdminAsync()
    {
        var response = await _doctorService.GetAllForAdminAsync();
        return Ok(response);
    }

    [HttpPut("admin/{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DoctorProfile>> SetActiveStatusAsync(Guid id, [FromBody] SetActiveStatusRequest request)
    {
        try
        {
            var response = await _doctorService.SetActiveStatusAsync(id, request.IsActive);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Doctor profile not found." });
        }
    }
}
