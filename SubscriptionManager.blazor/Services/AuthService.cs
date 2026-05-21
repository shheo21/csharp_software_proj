using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using SubscriptionManager.blazor.Models;

namespace SubscriptionManager.blazor.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error)> LoginAsync(LoginRequest request);
    Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
    Task<UserInfo?> GetCurrentUserAsync();
}

public class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly CustomAuthStateProvider _authStateProvider;

    private const string TOKEN_KEY = "subtrack_token";
    private const string USER_KEY = "subtrack_user";

    public AuthService(HttpClient http, IJSRuntime js, CustomAuthStateProvider authStateProvider)
    {
        _http = http;
        _js = js;
        _authStateProvider = authStateProvider;
    }

    public async Task<(bool Success, string? Error)> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", request);
            if (!response.IsSuccessStatusCode)
                return (false, await TryParseError(response) ?? "로그인에 실패했습니다.");

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth == null) return (false, "응답 처리 오류");

            await SaveUserAsync(auth);
            _authStateProvider.NotifyAuthStateChanged();

            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/register", request);
            if (!response.IsSuccessStatusCode)
                return (false, await TryParseError(response) ?? "회원가입에 실패했습니다.");
            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task LogoutAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", TOKEN_KEY);
        await _js.InvokeVoidAsync("localStorage.removeItem", USER_KEY);
        _authStateProvider.NotifyAuthStateChanged();
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", USER_KEY);
            if (string.IsNullOrWhiteSpace(json)) return null;

            var user = JsonSerializer.Deserialize<UserInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (user == null || user.ExpiresAt < DateTime.UtcNow) return null;

            return user;
        }
        catch { return null; }
    }

    private async Task SaveUserAsync(AuthResponse auth)
    {
        var userInfo = new UserInfo
        {
            DisplayName = auth.DisplayName,
            Email = auth.Email,
            Token = auth.Token,
            RefreshToken = auth.RefreshToken,
            ExpiresAt = auth.ExpiresAt
        };
        await _js.InvokeVoidAsync("localStorage.setItem", TOKEN_KEY, auth.Token);
        await _js.InvokeVoidAsync("localStorage.setItem", USER_KEY,
            JsonSerializer.Serialize(userInfo));
    }

    private static async Task<string?> TryParseError(HttpResponseMessage response)
    {
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("message", out var msg)) return msg.GetString();
        }
        catch { }
        return null;
    }
}
