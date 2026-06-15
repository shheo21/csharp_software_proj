using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using SubscriptionManager.blazor.Services;

namespace SubscriptionManager.blazor;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        var apiBaseUrl = NormalizeApiBaseUrl(builder.Configuration["ApiBaseUrl"]);

        builder.Services.AddScoped<AuthMessageHandler>();
        builder.Services.AddScoped(sp =>
        {
            var authHandler = sp.GetRequiredService<AuthMessageHandler>();
            authHandler.InnerHandler = new HttpClientHandler();
            return new HttpClient(authHandler)
            {
                BaseAddress = apiBaseUrl
            };
        });

        // 로그인 제공자 추가
        builder.Services.AddScoped<CustomAuthStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(
            sp => sp.GetRequiredService<CustomAuthStateProvider>());
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddAuthorizationCore();

        // API 서비스
        builder.Services.AddScoped<ISubscriptionApiService, SubscriptionApiService>();
        builder.Services.AddScoped<ICategoryService, CategoryService>();

        // 테마 (라이트/다크/시스템)
        builder.Services.AddScoped<IThemeService, ThemeService>();

        // MudBlazor
        builder.Services.AddMudServices();

        await builder.Build().RunAsync();
    }

    private static Uri NormalizeApiBaseUrl(string? apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            throw new InvalidOperationException("ApiBaseUrl이 설정되지 않았습니다.");

        var normalized = apiBaseUrl.Trim().TrimEnd('/') + "/";
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"ApiBaseUrl이 올바른 URL이 아닙니다: {apiBaseUrl}");

        return uri;
    }
}
