using System.Net.Http.Json;
using SubscriptionManager.blazor.Models;

namespace SubscriptionManager.blazor.Services;

public interface ISubscriptionApiService
{
    Task<List<SubscriptionDto>> GetSubscriptionsAsync(string? search = null, string? category = null, string? currency = null);
    Task<SubscriptionDto?> GetByIdAsync(int id);
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

    public async Task<SubscriptionDto?> GetByIdAsync(int id) =>
        await _http.GetFromJsonAsync<SubscriptionDto>($"api/subscriptions/{id}");

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

    public async Task<DashboardSummary> GetDashboardAsync() =>
        (await _http.GetFromJsonAsync<DashboardSummary>("api/subscriptions/dashboard"))!;

    public async Task<List<ExchangeRateSummary>> GetExchangeRatesAsync() =>
        await _http.GetFromJsonAsync<List<ExchangeRateSummary>>("api/exchangerate") ?? new();

    public async Task<List<string>> GetCategoriesAsync() =>
        await _http.GetFromJsonAsync<List<string>>("api/subscriptions/categories") ?? new();

    public async Task<SpendingTrendsDto> GetSpendingTrendsAsync(int months = 12) =>
        (await _http.GetFromJsonAsync<SpendingTrendsDto>($"api/subscriptions/spending-trends?months={months}"))!;
}
