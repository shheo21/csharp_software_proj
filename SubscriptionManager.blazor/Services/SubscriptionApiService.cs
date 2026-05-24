using System.Net.Http.Json;
using SubscriptionManager.blazor.Models;

namespace SubscriptionManager.blazor.Services;

public interface ISubscriptionApiService
{
    Task<List<SubscriptionDto>> GetSubscriptionsAsync(string? search = null, string? category = null, string? currency = null);
    Task<SubscriptionDto> CreateAsync(CreateSubscriptionRequest request);
    Task<SubscriptionDto?> UpdateAsync(int id, UpdateSubscriptionRequest request);
    Task DeleteAsync(int id);
    Task<DashboardSummary> GetDashboardAsync();
    Task<List<ExchangeRateSummary>> GetExchangeRatesAsync();
    Task<List<string>> GetCategoriesAsync();
    Task<SpendingTrendsDto> GetSpendingTrendsAsync(int months = 12);
}

public class SubscriptionApiService : ISubscriptionApiService
{
    private readonly HttpClient _http;

    public SubscriptionApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<SubscriptionDto>> GetSubscriptionsAsync(string? search = null, string? category = null, string? currency = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
        if (!string.IsNullOrWhiteSpace(currency)) query.Add($"currency={Uri.EscapeDataString(currency)}");

        var url = "api/subscriptions" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await _http.GetFromJsonAsync<List<SubscriptionDto>>(url) ?? new();
    }

    public async Task<SubscriptionDto> CreateAsync(CreateSubscriptionRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/subscriptions", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubscriptionDto>())!;
    }

    public async Task<SubscriptionDto?> UpdateAsync(int id, UpdateSubscriptionRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/subscriptions/{id}", request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubscriptionDto>();
    }

    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/subscriptions/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<DashboardSummary> GetDashboardAsync()
    {
        var subs = await GetSubscriptionsAsync();
        var active = subs.Where(s => s.IsActive).ToList();

        var totalMonthly = active.Sum(s => s.MonthlyAmountInKRW);

        var categoryBreakdown = active
            .GroupBy(s => s.Category)
            .Select(g => new CategorySpend
            {
                Category = g.Key,
                MonthlyAmountKRW = g.Sum(x => x.MonthlyAmountInKRW),
                Count = g.Count(),
            })
            .OrderByDescending(c => c.MonthlyAmountKRW)
            .ToList();

        return new DashboardSummary
        {
            TotalMonthlyKRW = totalMonthly,
            TotalYearlyKRW = totalMonthly * 12,
            ActiveCount = active.Count,
            UpcomingBilling = active.OrderBy(s => s.DaysUntilBilling).ToList(),
            CategoryBreakdown = categoryBreakdown,
        };
    }

    public async Task<List<ExchangeRateSummary>> GetExchangeRatesAsync() =>
        await _http.GetFromJsonAsync<List<ExchangeRateSummary>>("api/exchangerate") ?? new();

    public async Task<List<string>> GetCategoriesAsync() =>
    await _http.GetFromJsonAsync<List<string>>("api/subscriptions/categories") ?? new();

    // 백엔드에 결제 이력 테이블이 없어 진짜 과거 트렌드는 만들 수 없음.
    // 현재 활성 구독을 모든 월에 동일한 금액으로 채운 평면 트렌드를 반환.
    public async Task<SpendingTrendsDto> GetSpendingTrendsAsync(int months = 12)
    {
        var subs = await GetSubscriptionsAsync();
        var active = subs.Where(s => s.IsActive).ToList();
        var totalMonthly = active.Sum(s => s.MonthlyAmountInKRW);

        var categoryBreakdown = active
            .GroupBy(s => s.Category)
            .Select(g => new CategorySpend
            {
                Category = g.Key,
                MonthlyAmountKRW = g.Sum(x => x.MonthlyAmountInKRW),
                Count = g.Count(),
            })
            .OrderByDescending(c => c.MonthlyAmountKRW)
            .ToList();

        var byCategory = categoryBreakdown
            .Select(c => new CategoryMonthlyAmount
            {
                Category = c.Category,
                AmountKRW = c.MonthlyAmountKRW,
            })
            .ToList();

        var monthlyTrends = new List<MonthlyTrend>(months);
        var thisMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        for (var i = months - 1; i >= 0; i--)
        {
            var m = thisMonth.AddMonths(-i);
            monthlyTrends.Add(new MonthlyTrend
            {
                Month = m.ToString("yyyy-MM"),
                Label = m.ToString("M월"),
                TotalKRW = totalMonthly,
                ByCategory = byCategory,
            });
        }

        return new SpendingTrendsDto
        {
            MonthlyTrends = monthlyTrends,
            CategoryBreakdown = categoryBreakdown,
            TotalMonthlyKRW = totalMonthly,
            TotalYearlyKRW = totalMonthly * 12,
            ActiveCount = active.Count,
            TopCategory = categoryBreakdown.FirstOrDefault()?.Category ?? string.Empty,
            AverageMonthlyKRW = totalMonthly,
        };
    }
}
