using Microsoft.EntityFrameworkCore;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Services;

public class SubscriptionService
{
    private readonly AppDbContext _db;

    public SubscriptionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<SubscriptionDto>> GetSubscriptionsAsync(string userId, SubscriptionQueryParams query)
    {
        var q = _db.Subscriptions.Where(s => s.UserId == userId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            q = q.Where(s =>
                s.Name.ToLower().Contains(term) ||
                s.Category.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
            q = q.Where(s => s.Category == query.Category.Trim());

        if (!string.IsNullOrWhiteSpace(query.Currency))
            q = q.Where(s => s.Currency == query.Currency.Trim().ToUpper());

        var subscriptions = await q.OrderBy(s => s.NextBillingDate).ToListAsync();
        var exchangeRates = await LoadExchangeRatesAsync();

        return subscriptions.Select(s => ToDto(s, exchangeRates));
    }

    public async Task<SubscriptionDto> CreateSubscriptionAsync(string userId, CreateSubscriptionRequest req)
    {
        var subscription = new Subscription
        {
            UserId = userId,
            Name = req.Name.Trim(),
            Category = req.Category.Trim(),
            Amount = req.Amount,
            Currency = req.Currency.Trim().ToUpper(),
            BillingCycle = req.BillingCycle.ToUpper(),
            NextBillingDate = req.NextBillingDate.ToUniversalTime(),
            IconEmoji = req.IconEmoji,
            Notes = req.Notes?.Trim(),
            IsActive = true,
        };

        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();

        return ToDto(subscription, await LoadExchangeRatesAsync());
    }

    public async Task<SubscriptionDto?> UpdateSubscriptionAsync(string userId, int id, UpdateSubscriptionRequest req)
    {
        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return null;

        subscription.Name = req.Name.Trim();
        subscription.Category = req.Category.Trim();
        subscription.Amount = req.Amount;
        subscription.Currency = req.Currency.Trim().ToUpper();
        subscription.BillingCycle = req.BillingCycle.ToUpper();
        subscription.NextBillingDate = req.NextBillingDate.ToUniversalTime();
        subscription.IconEmoji = req.IconEmoji;
        subscription.Notes = req.Notes?.Trim();
        subscription.IsActive = req.IsActive;

        await _db.SaveChangesAsync();

        return ToDto(subscription, await LoadExchangeRatesAsync());
    }

    public async Task<bool> DeleteSubscriptionAsync(string userId, int id)
    {
        var subscription = await _db.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

        if (subscription == null)
            return false;

        _db.Subscriptions.Remove(subscription);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsCurrencySupportedAsync(string currency)
    {
        var code = currency.Trim().ToUpper();
        return code == "KRW" || await _db.ExchangeRates.AnyAsync(r => r.CurrencyCode == code);
    }

    private Task<Dictionary<string, decimal>> LoadExchangeRatesAsync() =>
        _db.ExchangeRates.ToDictionaryAsync(r => r.CurrencyCode, r => r.RateToKRW);

    private static SubscriptionDto ToDto(Subscription s, Dictionary<string, decimal> exchangeRates)
    {
        var today = DateTime.UtcNow.Date;
        var rateToKrw = s.Currency == "KRW" ? 1m : exchangeRates.GetValueOrDefault(s.Currency, 1m);
        var amountInKrw = Math.Round(s.Amount * rateToKrw, 0);
        var monthlyAmountInKrw = s.BillingCycle == "YEARLY"
            ? Math.Round(amountInKrw / 12, 0)
            : amountInKrw;

        return new SubscriptionDto
        {
            Id = s.Id,
            Name = s.Name,
            Category = s.Category,
            Amount = s.Amount,
            Currency = s.Currency,
            BillingCycle = s.BillingCycle,
            NextBillingDate = s.NextBillingDate,
            IconEmoji = s.IconEmoji,
            Notes = s.Notes,
            IsActive = s.IsActive,
            AmountInKRW = amountInKrw,
            MonthlyAmountInKRW = monthlyAmountInKrw,
            DaysUntilBilling = (s.NextBillingDate.Date - today).Days,
        };
    }
}
