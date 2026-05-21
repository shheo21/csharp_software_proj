using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace SubscriptionManager.blazor.Services;

public class AuthMessageHandler : DelegatingHandler
{
    private const string TOKEN_KEY = "subtrack_token";
    private readonly IJSRuntime _js;

    public AuthMessageHandler(IJSRuntime js)
    {
        _js = js;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization is null)
        {
            try
            {
                var token = await _js.InvokeAsync<string?>(
                    "localStorage.getItem", cancellationToken, TOKEN_KEY);
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            catch
            {
                // localStorage 접근 실패 시 토큰 없이 진행
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
