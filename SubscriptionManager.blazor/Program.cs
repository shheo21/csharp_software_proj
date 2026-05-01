using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace SubscriptionManager.blazor;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

        // 로그인 제공자 추가
        /* builder.Services.AddScoped<CustomAuthStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(
            sp => sp.GetRequiredService<CustomAuthStateProvider>());
        builder.Services.AddScoped<IAuthService, AuthService>(); */
        builder.Services.AddAuthorizationCore();

        await builder.Build().RunAsync();
    }
}
