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

    public async Task<(UserProfileResponse? profile, string? error)> UpdateProfileAsync(
        string userId,
        UpdateProfileRequest req)
    {
        var displayName = req.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return (null, "표시 이름을 입력해주세요.");

        if (displayName.Length > 100)
            return (null, "표시 이름은 100자 이하여야 합니다.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (null, "유저를 찾을 수 없습니다.");

        user.DisplayName = displayName;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return (null, string.Join(" ", result.Errors.Select(e => e.Description)));

        return (new UserProfileResponse
        {
            DisplayName = user.DisplayName,
            Email = user.Email!,
        }, null);
    }

    public async Task<(AuthResponse? auth, string? error)> ChangePasswordAsync(
        string userId,
        ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword) ||
            string.IsNullOrWhiteSpace(req.NewPassword))
            return (null, "현재 비밀번호와 새 비밀번호를 입력해주세요.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return (null, "유저를 찾을 수 없습니다.");

        var result = await _userManager.ChangePasswordAsync(
            user,
            req.CurrentPassword,
            req.NewPassword);

        if (!result.Succeeded)
            return (null, string.Join(" ", result.Errors.Select(e => e.Description)));

        await RevokeUserRefreshTokensAsync(userId);

        return (await IssueTokenPairAsync(user), null);
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

    private async Task RevokeUserRefreshTokensAsync(string userId)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        if (tokens.Count > 0)
            await _db.SaveChangesAsync();
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
