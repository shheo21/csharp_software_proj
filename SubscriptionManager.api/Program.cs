using Microsoft.EntityFrameworkCore;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;

var builder = WebApplication.CreateBuilder(args);

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=subscriptions.db"));

// ASP.NET Core Identity + EF Core 연동
builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

var app = builder.Build();

// 시작 시 DB 자동 생성 (마이그레이션 없이)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// /login, /register, /refresh, /logout 등 자동 생성
app.MapIdentityApi<ApplicationUser>();

app.Run();
