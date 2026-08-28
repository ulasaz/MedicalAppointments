using Identity.DTO_s;
using Identity.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System.Security.Claims;

namespace Identity.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly IStringLocalizer<AuthController> _localizer;

    public AuthController(IIdentityService identityService, IStringLocalizer<AuthController> localizer)
    {
        _identityService = identityService;
        _localizer = localizer;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (request == null)
        {
            throw new NullReferenceException();
        }
        try
        {
            var response = await _identityService.RegisterAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = _localizer["UserAlreadyExists"].Value });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = _localizer["InvalidRegistrationData"].Value });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (request == null)
        {
            throw new NullReferenceException();
        }
        try
        {
            var response = await _identityService.LoginAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { message = _localizer["InvalidCredentials"].Value });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = _localizer["AccountDeactivated"].Value });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = _localizer["InvalidLoginData"].Value });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        try
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
            {
                return Unauthorized();
            }

            var response = await _identityService.GetProfileAsync(userId);
            return Ok(response);
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { message = _localizer["UserNotFound"].Value });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = _localizer["InvalidProfileRequest"].Value });
        }
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _identityService.UpdateDisplayNameAsync(userId, request.DisplayName);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return Unauthorized();
        }
        catch (ArgumentException)
        {
            return BadRequest(new { message = _localizer["InvalidProfileRequest"].Value });
        }
    }

    [AllowAnonymous]
    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        try
        {
            var response = await _identityService.GetProfileAsync(id);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = _localizer["UserNotFound"].Value });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/users")]
    public async Task<ActionResult<List<UserProfileResponse>>> GetAllUsers()
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var requestingAdminId))
        {
            return Unauthorized();
        }

        var response = await _identityService.GetAllUsersAsync(requestingAdminId);
        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("admin/users/{id:guid}/status")]
    public async Task<IActionResult> SetUserStatus(Guid id, [FromBody] SetUserStatusRequest request)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var requestingAdminId))
        {
            return Unauthorized();
        }

        try
        {
            var response = await _identityService.SetUserActiveStatusAsync(requestingAdminId, id, request.IsActive);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = _localizer["UserNotFound"].Value });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { message = "An admin cannot change their own account status." });
        }
    }
}
