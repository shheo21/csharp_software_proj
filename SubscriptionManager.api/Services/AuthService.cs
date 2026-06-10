using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Services;

public class AuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(UserManager<ApplicationUser> userManager, AppDbContext db, IConfiguration config)
    {
        _userManager = userManager;
        _db = db;
        _config = config;
    }

    public async Task<(bool success, string? error)> RegisterAsync(RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.DisplayName) ||
            string.IsNullOrWhiteSpace(req.Email) ||
            string.IsNullOrWhiteSpace(req.Password))
            return (false, "모든 필드를 입력해주세요.");

        var user = new ApplicationUser
        {
            UserName = req.Email.Trim().ToLower(),
            Email = req.Email.Trim().ToLower(),
            DisplayName = req.DisplayName.Trim(),
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return (false, string.Join(" ", result.Errors.Select(e => e.Description)));

        return (true, null);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return null;

        var user = await _userManager.FindByEmailAsync(req.Email.Trim().ToLower());
        if (user == null || !await _userManager.CheckPasswordAsync(user, req.Password))
            return null;

        return await IssueTokenPairAsync(user);
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken)
    {
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (stored == null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow)
            return null;

        stored.IsRevoked = true;
        await _db.SaveChangesAsync();

        return await IssueTokenPairAsync(stored.User);
    }

    public async Task<MeResponse?> GetMeAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return null;

        return new MeResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email!,
            CreatedAt = user.CreatedAt,
        };
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (stored != null)
        {
            stored.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    private async Task<AuthResponse> IssueTokenPairAsync(ApplicationUser user)
    {
        var accessExpiresAt = DateTime.UtcNow.AddMinutes(15);
        var accessToken = GenerateJwt(user, accessExpiresAt);

        var refreshToken = new RefreshToken
        {
            Token = GenerateRefreshToken(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
        };
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return new AuthResponse
        {
            Token = accessToken,
            RefreshToken = refreshToken.Token,
            DisplayName = user.DisplayName,
            Email = user.Email!,
            ExpiresAt = accessExpiresAt,
        };
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

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
