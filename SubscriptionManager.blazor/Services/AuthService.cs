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
    Task<bool> RefreshAsync();
    Task<(bool Success, string? Error)> UpdateProfileAsync(UpdateProfileRequest request);
    Task<(bool Success, string? Error)> ChangePasswordAsync(ChangePasswordRequest request);
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
        try
        {
            var user = await GetCurrentUserFromStorageAsync();
            if (user != null && !string.IsNullOrWhiteSpace(user.RefreshToken))
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout")
                {
                    Content = JsonContent.Create(new RefreshRequest { RefreshToken = user.RefreshToken })
                };
                // 응답이 401 이어도 refresh 후 재시도할 의미가 없음 — 어차피 로컬은 정리됨
                request.Options.Set(AuthHttpRequestOptions.SkipAuthRetry, true);
                await _http.SendAsync(request);
            }
        }
        catch
        {
            // 서버 통신 실패해도 로컬 정리는 계속 진행
        }

        await _js.InvokeVoidAsync("localStorage.removeItem", TOKEN_KEY);
        await _js.InvokeVoidAsync("localStorage.removeItem", USER_KEY);
        _authStateProvider.NotifyAuthStateChanged();
    }

    public async Task<bool> RefreshAsync()
    {
        try
        {
            var user = await GetCurrentUserFromStorageAsync();
            if (user == null || string.IsNullOrWhiteSpace(user.RefreshToken)) return false;

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/refresh")
            {
                Content = JsonContent.Create(new RefreshRequest { RefreshToken = user.RefreshToken })
            };
            // 401 응답 시 무한 루프 방지 — refresh 요청 자체는 재시도하지 않음
            request.Options.Set(AuthHttpRequestOptions.SkipAuthRetry, true);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return false;

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth == null) return false;

            await SaveUserAsync(auth);
            _authStateProvider.NotifyAuthStateChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(UpdateProfileRequest request)
    {
        try
        {
            var response = await _http.PutAsJsonAsync("api/auth/profile", request);
            if (!response.IsSuccessStatusCode)
                return (false, await TryParseError(response) ?? "프로필 저장에 실패했습니다.");

            var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
            if (profile == null) return (false, "응답 처리 오류");

            await UpdateStoredProfileAsync(profile);
            _authStateProvider.NotifyAuthStateChanged();

            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(ChangePasswordRequest request)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/change-password", request);
            if (!response.IsSuccessStatusCode)
                return (false, await TryParseError(response) ?? "비밀번호 변경에 실패했습니다.");

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth == null) return (false, "응답 처리 오류");

            await SaveUserAsync(auth);
            _authStateProvider.NotifyAuthStateChanged();

            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        var user = await GetCurrentUserFromStorageAsync();
        if (user == null || user.ExpiresAt < DateTime.UtcNow) return null;
        return user;
    }

    private async Task<UserInfo?> GetCurrentUserFromStorageAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", USER_KEY);
            if (string.IsNullOrWhiteSpace(json)) return null;

            return JsonSerializer.Deserialize<UserInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
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

    private async Task UpdateStoredProfileAsync(UserProfileResponse profile)
    {
        var user = await GetCurrentUserFromStorageAsync();
        if (user == null) return;

        user.DisplayName = profile.DisplayName;
        user.Email = profile.Email;

        await _js.InvokeVoidAsync("localStorage.setItem", USER_KEY,
            JsonSerializer.Serialize(user));
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
