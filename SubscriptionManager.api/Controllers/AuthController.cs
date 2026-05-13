using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public AuthController(UserManager<ApplicationUser> userManager, IConfiguration config)
    {
        _userManager = userManager;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "모든 필드를 입력해주세요." });

        var user = new ApplicationUser
        {
            UserName = req.Email.Trim().ToLower(),
            Email = req.Email.Trim().ToLower(),
            DisplayName = req.DisplayName.Trim(),
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { message = string.Join(" ", errors) });
        }

        return Ok(new { message = "회원가입이 완료됐습니다." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "이메일과 비밀번호를 입력해주세요." });

        var user = await _userManager.FindByEmailAsync(req.Email.Trim().ToLower());
        if (user == null || !await _userManager.CheckPasswordAsync(user, req.Password))
            return Unauthorized(new { message = "이메일 또는 비밀번호가 올바르지 않습니다." });

        var expiresAt = DateTime.UtcNow.AddDays(7);
        var token = GenerateJwt(user, expiresAt);

        return Ok(new AuthResponse
        {
            Token = token,
            DisplayName = user.DisplayName,
            Email = user.Email!,
            ExpiresAt = expiresAt,
        });
    }

    private string GenerateJwt(ApplicationUser user, DateTime expiresAt)
    {
        var jwtKey = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key가 설정되지 않았습니다.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.Name, user.DisplayName),
        };

        var token = new JwtSecurityToken(
            issuer: "SubscriptionManager",
            audience: "SubscriptionManager",
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
