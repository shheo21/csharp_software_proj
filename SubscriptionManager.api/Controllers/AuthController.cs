using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.api.Models;
using SubscriptionManager.api.Services;

namespace SubscriptionManager.api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var (success, error) = await _authService.RegisterAsync(req);
        if (!success)
            return BadRequest(new { message = error });

        return Ok(new { message = "회원가입이 완료됐습니다." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "이메일과 비밀번호를 입력해주세요." });

        var result = await _authService.LoginAsync(req);
        if (result == null)
            return Unauthorized(new { message = "이메일 또는 비밀번호가 올바르지 않습니다." });

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var result = await _authService.RefreshAsync(req.RefreshToken);
        if (result == null)
            return Unauthorized(new { message = "유효하지 않은 Refresh Token입니다." });

        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _authService.GetMeAsync(userId!);
        if (result == null)
            return Unauthorized(new { message = "유저를 찾을 수 없습니다." });

        return Ok(result);
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized(new { message = "유저를 찾을 수 없습니다." });

        var (profile, error) = await _authService.UpdateProfileAsync(userId, req);
        if (profile == null)
            return BadRequest(new { message = error });

        return Ok(profile);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
            return Unauthorized(new { message = "유저를 찾을 수 없습니다." });

        var (auth, error) = await _authService.ChangePasswordAsync(userId, req);
        if (auth == null)
            return BadRequest(new { message = error });

        return Ok(auth);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        await _authService.RevokeRefreshTokenAsync(req.RefreshToken);
        return Ok(new { message = "로그아웃됐습니다." });
    }
}
