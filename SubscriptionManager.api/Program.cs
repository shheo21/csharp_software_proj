using Microsoft.AspNetCore.Identity;

namespace SubscriptionManager.api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        // 로그인 인증 서비스 등록 (JWT 발급 및 인증 시스템 세팅)
        /* builder.Services.AddIdentityApiEndpoints<IdentityUser>()
            .AddEntityFrameworkStores<MyDbContext>(); */

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        // API 엔드포인트 자동 생성 (/login, /register 등)
        app.MapIdentityApi<IdentityUser>();

        app.Run();
    }
}
