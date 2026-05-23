using System.Net;
using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace SubscriptionManager.blazor.Services;

public static class AuthHttpRequestOptions
{
    // refresh 자체 호출이나, 401 응답 시 재시도하면 안 되는 요청에 마킹.
    public static readonly HttpRequestOptionsKey<bool> SkipAuthRetry = new("__skip_auth_retry");
}

public class AuthMessageHandler : DelegatingHandler
{
    private const string TOKEN_KEY = "subtrack_token";

    private static readonly SemaphoreSlim _refreshLock = new(1, 1);

    private readonly IJSRuntime _js;
    private readonly IServiceProvider _services;

    public AuthMessageHandler(IJSRuntime js, IServiceProvider services)
    {
        _js = js;
        _services = services;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await AttachTokenAsync(request, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;
        if (request.Options.TryGetValue(AuthHttpRequestOptions.SkipAuthRetry, out var skip) && skip)
            return response;

        var oldToken = request.Headers.Authorization?.Parameter;
        var refreshedToken = await TryRefreshAsync(oldToken, cancellationToken);
        if (refreshedToken == null) return response;

        response.Dispose();
        var retry = await CloneRequestAsync(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshedToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task AttachTokenAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Headers.Authorization is not null) return;
        try
        {
            var token = await _js.InvokeAsync<string?>("localStorage.getItem", ct, TOKEN_KEY);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        catch
        {
            // localStorage 접근 실패 시 토큰 없이 진행
        }
    }

    // 다른 요청이 이미 refresh 를 마쳤다면 새 토큰만 반환. 아직 안 했다면 직접 refresh 시도.
    private async Task<string?> TryRefreshAsync(string? oldToken, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            var current = await ReadTokenAsync(ct);
            if (!string.IsNullOrWhiteSpace(current) && current != oldToken)
                return current;

            var auth = _services.GetRequiredService<IAuthService>();
            var success = await auth.RefreshAsync();
            if (!success)
            {
                await auth.LogoutAsync();
                return null;
            }

            return await ReadTokenAsync(ct);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<string?> ReadTokenAsync(CancellationToken ct)
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", ct, TOKEN_KEY); }
        catch { return null; }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage src)
    {
        var clone = new HttpRequestMessage(src.Method, src.RequestUri);

        if (src.Content != null)
        {
            var buffer = await src.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(buffer);
            foreach (var h in src.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        foreach (var h in src.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);

        foreach (var opt in src.Options)
            ((IDictionary<string, object?>)clone.Options)[opt.Key] = opt.Value;

        return clone;
    }
}
