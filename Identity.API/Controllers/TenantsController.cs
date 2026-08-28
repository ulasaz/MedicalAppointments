using System.Security.Claims;
using Identity.DTO_s;
using Identity.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Controllers;

[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    // Anonymous: the registration form and pre-login branding both need this
    // before a JWT (and therefore a tenant claim) exists yet.
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<MedicalCenterDto>>> GetAll()
    {
        var response = await _tenantService.GetAllAsync();
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var response = await _tenantService.GetByIdAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMedicalCenterRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var requestingAdminId))
        {
            return Unauthorized();
        }

        // Only the platform super-admin (no tenant claim) may create new centers.
        if (User.HasClaim(c => c.Type == "tenant_id"))
        {
            return Forbid();
        }

        try
        {
            var response = await _tenantService.CreateAsync(request);
            return CreatedAtAction("GetById", new { id = response.Id }, response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMedicalCenterRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var requestingAdminId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _tenantService.UpdateAsync(requestingAdminId, id, request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/banner")]
    public async Task<IActionResult> GetBanner(Guid id)
    {
        try
        {
            var (data, contentType) = await _tenantService.GetBannerAsync(id);
            return File(data, contentType);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:guid}/banner")]
    public async Task<IActionResult> UploadBanner(Guid id, IFormFile file)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var requestingAdminId))
        {
            return Unauthorized();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No image was uploaded." });
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        try
        {
            await _tenantService.UploadBannerAsync(requestingAdminId, id, stream.ToArray(), file.ContentType);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}/banner")]
    public async Task<IActionResult> DeleteBanner(Guid id)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var requestingAdminId))
        {
            return Unauthorized();
        }

        try
        {
            await _tenantService.DeleteBannerAsync(requestingAdminId, id);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
