using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;
using SubscriptionManager.api.Services;

var builder = WebApplication.CreateBuilder(args);

// EF Core + SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=subscriptions.db"));

// ASP.NET Core Identity + EF Core 연동
builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<AppDbContext>();

// JWT 인증 (키는 appsettings.Development.json에서 읽음, .gitignore됨)
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key가 설정되지 않았습니다. appsettings.Development.json을 확인하세요.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "SubscriptionManager",
            ValidateAudience = true,
            ValidAudience = "SubscriptionManager",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddCors();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ExchangeRateService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISubscriptionCalculationService, SubscriptionCalculationService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISpendingAnalysisService, SpendingAnalysisService>();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SubscriptionManager API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "로그인 후 발급된 Access Token을 입력하세요.",
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 시작 시 DB 자동 생성 + Mock 유저 시드
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    const string mockEmail = "test@example.com";

    if (await userManager.FindByEmailAsync(mockEmail) == null)
    {
        var mockUser = new ApplicationUser
        {
            UserName = mockEmail,
            Email = mockEmail,
            DisplayName = "테스트유저",
        };
        await userManager.CreateAsync(mockUser, "Test1234!");
    }

    // 환율 시드
    if (!db.ExchangeRates.Any())
    {
        db.ExchangeRates.AddRange(
            new ExchangeRate { CurrencyCode = "USD", CurrencyName = "미국 달러", RateToKRW = 1380m, UpdatedAt = DateTime.UtcNow },
            new ExchangeRate { CurrencyCode = "EUR", CurrencyName = "유로", RateToKRW = 1520m, UpdatedAt = DateTime.UtcNow },
            new ExchangeRate { CurrencyCode = "JPY", CurrencyName = "일본 엔", RateToKRW = 9.2m, UpdatedAt = DateTime.UtcNow },
            new ExchangeRate { CurrencyCode = "GBP", CurrencyName = "영국 파운드", RateToKRW = 1750m, UpdatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
    }

    // Mock 구독 시드
    var seedUser = await userManager.FindByEmailAsync(mockEmail);
    if (seedUser != null && !db.Subscriptions.Any(s => s.UserId == seedUser.Id))
    {
        var today = DateTime.UtcNow.Date;
        db.Subscriptions.AddRange(
            new Subscription
            {
                UserId = seedUser.Id,
                Name = "Netflix",
                Category = "엔터테인먼트",
                Amount = 17000m,
                Currency = "KRW",
                BillingCycle = "MONTHLY",
                NextBillingDate = today.AddDays(12),
                IconEmoji = "🎬",
                Notes = "가족 요금제",
                IsActive = true,
            },
            new Subscription
            {
                UserId = seedUser.Id,
                Name = "Spotify",
                Category = "음악",
                Amount = 10.99m,
                Currency = "USD",
                BillingCycle = "MONTHLY",
                NextBillingDate = today.AddDays(5),
                IconEmoji = "🎵",
                IsActive = true,
            },
            new Subscription
            {
                UserId = seedUser.Id,
                Name = "iCloud",
                Category = "클라우드 스토리지",
                Amount = 1.29m,
                Currency = "USD",
                BillingCycle = "MONTHLY",
                NextBillingDate = today.AddDays(20),
                IconEmoji = "☁️",
                IsActive = true,
            },
            new Subscription
            {
                UserId = seedUser.Id,
                Name = "Adobe Creative Cloud",
                Category = "생산성",
                Amount = 54.99m,
                Currency = "USD",
                BillingCycle = "MONTHLY",
                NextBillingDate = today.AddDays(3),
                IconEmoji = "🎨",
                IsActive = true,
            }
        );
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SubscriptionManager API v1"));
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    // Blazor 측에서 API 호출 허용
    app.UseCors(p => p
    .WithOrigins("https://localhost:7000", "http://localhost:5030")
    .AllowAnyHeader()
    .AllowAnyMethod());
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
