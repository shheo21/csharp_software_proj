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

        builder.Services.AddScoped(sp => new HttpClient
        {
            BaseAddress = new Uri("https://localhost:7188")
        });

        // 로그인 제공자 추가
        /* builder.Services.AddScoped<CustomAuthStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(
            sp => sp.GetRequiredService<CustomAuthStateProvider>());
        builder.Services.AddScoped<IAuthService, AuthService>(); */
        builder.Services.AddAuthorizationCore();

        // API 서비스
        builder.Services.AddScoped<ISubscriptionApiService, SubscriptionApiService>();

        // MudBlazor
        builder.Services.AddMudServices();

        await builder.Build().RunAsync();
    }
}
