using Microsoft.EntityFrameworkCore;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Services;

public interface ISubscriptionCalculationService
{
    Task<Dictionary<string, decimal>> LoadExchangeRatesAsync();
    SubscriptionDto ToDto(
        Subscription subscription,
        Dictionary<string, decimal> exchangeRates);

    DateTime NormalizeNextBillingDate(
        DateTime nextBillingDate,
        string billingCycle,
        DateTime today);
}

public class SubscriptionCalculationService : ISubscriptionCalculationService
{
    private readonly AppDbContext _db;

    public SubscriptionCalculationService(AppDbContext db)
    {
        _db = db;
    }

    public Task<Dictionary<string, decimal>> LoadExchangeRatesAsync()
    {
        return _db.ExchangeRates
            .AsNoTracking()
            .ToDictionaryAsync(
                r => r.CurrencyCode,
                r => r.RateToKRW);
    }

    // SubscriptionController의 ToDto()와 유사한 로직이지만, SpendingAnalysisService에서도 필요하기 때문에 별도 서비스로 분리
    public SubscriptionDto ToDto(
        Subscription subscription,
        Dictionary<string, decimal> exchangeRates)
    {
        var today = DateTime.UtcNow.Date;

        var nextBillingDate = NormalizeNextBillingDate(
            subscription.NextBillingDate,
            subscription.BillingCycle,
            today);

        var currency = subscription.Currency.Trim().ToUpperInvariant();

        var rateToKrw = currency == "KRW"
            ? 1m
            : exchangeRates.GetValueOrDefault(currency, 1m);

        var amountInKrw = Math.Round(subscription.Amount * rateToKrw, 0);

        var billingCycle = subscription.BillingCycle.ToUpperInvariant();

        var monthlyAmountInKrw = billingCycle == "YEARLY"
            ? Math.Round(amountInKrw / 12m, 0)
            : amountInKrw;

        return new SubscriptionDto
        {
            Id = subscription.Id,
            Name = subscription.Name,
            Category = subscription.Category,
            Amount = subscription.Amount,
            Currency = subscription.Currency,
            BillingCycle = subscription.BillingCycle,
            StartDate = subscription.StartDate.Date,
            NextBillingDate = nextBillingDate,
            IconEmoji = subscription.IconEmoji,
            Notes = subscription.Notes,
            IsActive = subscription.IsActive,
            AmountInKRW = amountInKrw,
            MonthlyAmountInKRW = monthlyAmountInKrw,
            DaysUntilBilling = (nextBillingDate.Date - today).Days
        };
    }

    public DateTime NormalizeNextBillingDate(
        DateTime nextBillingDate,
        string billingCycle,
        DateTime today)
    {
        var date = nextBillingDate.Date;
        var cycle = billingCycle.ToUpperInvariant();

        while (date < today.Date)
        {
            date = cycle switch
            {
                "YEARLY" => date.AddYears(1),
                "MONTHLY" => date.AddMonths(1),
                _ => date.AddMonths(1)
            };
        }

        return date;
    }
}
