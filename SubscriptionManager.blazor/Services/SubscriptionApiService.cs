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
    Task<SpendingTrendsDto> GetSpendingTrendsAsync(int months = 12);
    Task<List<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false);
    Task<int> GetUnreadNotificationCountAsync();
    Task MarkNotificationAsReadAsync(int id);
    Task MarkAllNotificationsAsReadAsync();
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
        var subscriptions = await _http.GetFromJsonAsync<List<SubscriptionDto>>(url) ?? new();

        await RenewOverdueBillingDatesAsync(subscriptions);

        return subscriptions;
    }

    public async Task<SubscriptionDto> CreateAsync(CreateSubscriptionRequest request)
    {
        request.StartDate = ToApiDate(request.StartDate);
        request.NextBillingDate = ToApiDate(request.NextBillingDate);
        var response = await _http.PostAsJsonAsync("api/subscriptions", request);
        response.EnsureSuccessStatusCode();
        return NormalizeSubscription((await response.Content.ReadFromJsonAsync<SubscriptionDto>())!);
    }

    public async Task<SubscriptionDto?> UpdateAsync(int id, UpdateSubscriptionRequest request)
    {
        request.StartDate = ToApiDate(request.StartDate);
        request.NextBillingDate = ToApiDate(request.NextBillingDate);
        var response = await _http.PutAsJsonAsync($"api/subscriptions/{id}", request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var subscription = await response.Content.ReadFromJsonAsync<SubscriptionDto>();
        return subscription == null ? null : NormalizeSubscription(subscription);
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

    public async Task<SpendingTrendsDto> GetSpendingTrendsAsync(int months = 12)
    {
        var boundedMonths = Math.Clamp(months, 1, 24);
        return await _http.GetFromJsonAsync<SpendingTrendsDto>(
            $"api/subscriptions/spending-trends?months={boundedMonths}") ?? new();
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false)
    {
        var url = unreadOnly ? "api/notifications?unreadOnly=true" : "api/notifications";
        return await _http.GetFromJsonAsync<List<NotificationDto>>(url) ?? new();
    }

    public async Task<int> GetUnreadNotificationCountAsync()
    {
        var response = await _http.GetFromJsonAsync<UnreadCountResponse>("api/notifications/unread-count");
        return response?.Count ?? 0;
    }

    public async Task MarkNotificationAsReadAsync(int id)
    {
        var response = await _http.PatchAsync($"api/notifications/{id}/read", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkAllNotificationsAsReadAsync()
    {
        var response = await _http.PatchAsync("api/notifications/read-all", null);
        response.EnsureSuccessStatusCode();
    }

    private async Task RenewOverdueBillingDatesAsync(List<SubscriptionDto> subscriptions)
    {
        var today = DateTime.Today;
        var renewedSubscriptions = new List<SubscriptionDto>();

        foreach (var subscription in subscriptions)
        {
            var originalDate = subscription.NextBillingDate.Date;
            NormalizeSubscription(subscription, today);

            if (subscription.IsActive && subscription.NextBillingDate.Date != originalDate)
            {
                renewedSubscriptions.Add(subscription);
            }
        }

        foreach (var subscription in renewedSubscriptions)
        {
            await TryPersistRenewedBillingDateAsync(subscription);
        }
    }

    private async Task TryPersistRenewedBillingDateAsync(SubscriptionDto subscription)
    {
        try
        {
            var request = new UpdateSubscriptionRequest
            {
                Name = subscription.Name,
                Category = subscription.Category,
                Amount = subscription.Amount,
                Currency = subscription.Currency,
                BillingCycle = subscription.BillingCycle,
                StartDate = ToApiDate(subscription.StartDate),
                NextBillingDate = subscription.NextBillingDate,
                IconEmoji = subscription.IconEmoji,
                Notes = subscription.Notes,
                IsActive = subscription.IsActive
            };

            var response = await _http.PutAsJsonAsync($"api/subscriptions/{subscription.Id}", request);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var updated = await response.Content.ReadFromJsonAsync<SubscriptionDto>();
            if (updated != null)
            {
                CopySubscriptionValues(updated, subscription);
                NormalizeSubscription(subscription);
            }
        }
        catch
        {
            // 백엔드에서 구독 목록 저장 실패 시 현재 UI에서만 반영
        }
    }

    private static SubscriptionDto NormalizeSubscription(SubscriptionDto subscription)
    {
        NormalizeSubscription(subscription, DateTime.Today);
        return subscription;
    }

    private static void NormalizeSubscription(SubscriptionDto subscription, DateTime today)
    {
        if (subscription.IsActive)
        {
            subscription.NextBillingDate = NormalizeNextBillingDate(
                subscription.NextBillingDate,
                subscription.BillingCycle,
                today);
        }

        subscription.DaysUntilBilling = (subscription.NextBillingDate.Date - today.Date).Days;
    }

    private static DateTime NormalizeNextBillingDate(
        DateTime nextBillingDate,
        string billingCycle,
        DateTime today)
    {
        var date = nextBillingDate.Date;
        if (date == default)
        {
            return today.Date;
        }

        var cycle = billingCycle.Trim().ToUpperInvariant();
        var guard = 0;

        while (date < today.Date && guard++ < 1200)
        {
            date = cycle switch
            {
                "YEARLY" => date.AddYears(1),
                _ => date.AddMonths(1)
            };
        }

        return date;
    }

    private static void CopySubscriptionValues(SubscriptionDto source, SubscriptionDto target)
    {
        target.Id = source.Id;
        target.Name = source.Name;
        target.Category = source.Category;
        target.Amount = source.Amount;
        target.Currency = source.Currency;
        target.BillingCycle = source.BillingCycle;
        target.StartDate = source.StartDate;
        target.NextBillingDate = source.NextBillingDate;
        target.IconEmoji = source.IconEmoji;
        target.Notes = source.Notes;
        target.IsActive = source.IsActive;
        target.AmountInKRW = source.AmountInKRW;
        target.MonthlyAmountInKRW = source.MonthlyAmountInKRW;
        target.DaysUntilBilling = source.DaysUntilBilling;
    }

    private static DateTime ToApiDate(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    }
}
