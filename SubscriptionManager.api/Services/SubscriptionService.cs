using Microsoft.EntityFrameworkCore;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Services;

public class SubscriptionService
{
    private readonly AppDbContext _db;
    private readonly ISubscriptionCalculationService _calculationService;

    public SubscriptionService(
        AppDbContext db,
        ISubscriptionCalculationService calculationService)
    {
        _db = db;
        _calculationService = calculationService;
    }

    public async Task<IEnumerable<SubscriptionDto>> GetSubscriptionsAsync(string userId, SubscriptionQueryParams query)
    {
        await RenewOverdueBillingDatesAsync(userId);

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
        var exchangeRates = await _calculationService.LoadExchangeRatesAsync();

        return subscriptions
            .Select(s => _calculationService.ToDto(s, exchangeRates))
            .OrderBy(s => s.NextBillingDate);
    }

    public async Task<SubscriptionDto> CreateSubscriptionAsync(string userId, CreateSubscriptionRequest req)
    {
        var billingCycle = req.BillingCycle.Trim().ToUpper();
        var subscription = new Subscription
        {
            UserId = userId,
            Name = req.Name.Trim(),
            Category = req.Category.Trim(),
            Amount = req.Amount,
            Currency = req.Currency.Trim().ToUpper(),
            BillingCycle = billingCycle,
            NextBillingDate = NormalizeBillingDate(req.NextBillingDate, billingCycle),
            IconEmoji = req.IconEmoji,
            Notes = req.Notes?.Trim(),
            IsActive = true,
        };

        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();

        return _calculationService.ToDto(
            subscription,
            await _calculationService.LoadExchangeRatesAsync());
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
        subscription.BillingCycle = req.BillingCycle.Trim().ToUpper();
        subscription.NextBillingDate = req.IsActive
            ? NormalizeBillingDate(req.NextBillingDate, subscription.BillingCycle)
            : ToStorageDate(req.NextBillingDate);
        subscription.IconEmoji = req.IconEmoji;
        subscription.Notes = req.Notes?.Trim();
        subscription.IsActive = req.IsActive;

        await _db.SaveChangesAsync();

        return _calculationService.ToDto(
            subscription,
            await _calculationService.LoadExchangeRatesAsync());
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

    private async Task RenewOverdueBillingDatesAsync(string userId)
    {
        var today = DateTime.UtcNow.Date;
        var changed = false;
        var subscriptions = await _db.Subscriptions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var subscription in subscriptions)
        {
            var normalizedDate = _calculationService.NormalizeNextBillingDate(
                subscription.NextBillingDate,
                subscription.BillingCycle,
                today);

            if (normalizedDate.Date == subscription.NextBillingDate.Date)
            {
                continue;
            }

            subscription.NextBillingDate = ToStorageDate(normalizedDate);
            changed = true;
        }

        if (changed)
        {
            await _db.SaveChangesAsync();
        }
    }

    private DateTime NormalizeBillingDate(DateTime nextBillingDate, string billingCycle)
    {
        var normalizedDate = _calculationService.NormalizeNextBillingDate(
            ToStorageDate(nextBillingDate),
            billingCycle,
            DateTime.UtcNow.Date);

        return ToStorageDate(normalizedDate);
    }

    private static DateTime ToStorageDate(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    }
}
