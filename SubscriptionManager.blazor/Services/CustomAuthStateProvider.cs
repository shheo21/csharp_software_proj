using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace SubscriptionManager.blazor.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _js;
    private const string TOKEN_KEY = "subtrack_token";

    public CustomAuthStateProvider(IJSRuntime js)
    {
        _js = js;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", TOKEN_KEY);
            if (string.IsNullOrWhiteSpace(token))
                return Unauthenticated();

            var claims = ParseJwtClaims(token);
            if (claims == null || IsTokenExpired(token))
            {
                await _js.InvokeVoidAsync("localStorage.removeItem", TOKEN_KEY);
                await _js.InvokeVoidAsync("localStorage.removeItem", "subtrack_user");
                return Unauthenticated();
            }

            var identity = new ClaimsIdentity(claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Unauthenticated();
        }
    }

    public void NotifyAuthStateChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    private static AuthenticationState Unauthenticated()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));

    private static List<Claim>? ParseJwtClaims(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (dict == null) return null;

            var claims = new List<Claim>();

            if (dict.TryGetValue("nameid", out var nameId) ||
                dict.TryGetValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", out nameId) ||
                dict.TryGetValue("sub", out nameId))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, nameId.GetString() ?? nameId.GetRawText()));

            if (dict.TryGetValue("email", out var email) ||
                dict.TryGetValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", out email))
                claims.Add(new Claim(ClaimTypes.Email, email.GetString() ?? ""));

            if (dict.TryGetValue("unique_name", out var name) ||
                dict.TryGetValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", out name) ||
                dict.TryGetValue("name", out name))
                claims.Add(new Claim(ClaimTypes.Name, name.GetString() ?? ""));

            return claims.Count > 0 ? claims : null;
        }
        catch { return null; }
    }

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

            if (dict != null && dict.TryGetValue("exp", out var exp))
                return DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()) < DateTimeOffset.UtcNow;

            return true;
        }
        catch { return true; }
    }
}
