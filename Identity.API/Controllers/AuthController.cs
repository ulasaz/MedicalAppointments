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
}
