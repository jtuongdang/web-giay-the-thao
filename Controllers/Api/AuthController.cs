using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyBanGiay.Models.DTOs;
using QuanLyBanGiay.Services.Auth;

namespace QuanLyBanGiay.Controllers.Api;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        if (!result.Succeeded || result.Response is null)
            return StatusCode(result.StatusCode, ApiResponse<AuthResponse>.Fail(result.Message, result.StatusCode));

        var user = result.Response.User;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("full_name", user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

        return StatusCode(result.StatusCode, ApiResponse<AuthResponse>.Ok(result.Response, result.Message, result.StatusCode));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        if (!result.Succeeded || result.Response is null)
            return StatusCode(result.StatusCode, ApiResponse<AuthResponse>.Fail(result.Message, result.StatusCode));

        return StatusCode(result.StatusCode, ApiResponse<AuthResponse>.Ok(result.Response, result.Message, result.StatusCode));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public IActionResult Me()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? string.Empty;
        var profile = new UserProfileDto
        {
            Id = int.TryParse(idValue, out var id) ? id : 0,
            Username = User.Identity?.Name ?? string.Empty,
            Email = email,
            FullName = User.FindFirstValue("full_name") ?? User.Identity?.Name ?? string.Empty,
            Role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty
        };
        return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Current user loaded."));
    }
}
