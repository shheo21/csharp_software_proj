using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public SubscriptionsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubscriptions([FromQuery] SubscriptionQueryParams query)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var exchangeRates = await _db.ExchangeRates
            .ToDictionaryAsync(r => r.CurrencyCode, r => r.RateToKRW);

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

        var subscriptions = await q
            .OrderBy(s => s.NextBillingDate)
            .ToListAsync();

        return Ok(subscriptions.Select(s => ToDto(s, exchangeRates)));
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var currency = req.Currency.Trim().ToUpper();
        if (currency != "KRW")
        {
            var exists = await _db.ExchangeRates.AnyAsync(r => r.CurrencyCode == currency);
            if (!exists)
                return BadRequest(new { message = $"지원하지 않는 통화 코드입니다: {currency}" });
        }

        var subscription = new Subscription
        {
            UserId = userId,
            Name = req.Name.Trim(),
            Category = req.Category.Trim(),
            Amount = req.Amount,
            Currency = currency,
            BillingCycle = req.BillingCycle.ToUpper(),
            NextBillingDate = req.NextBillingDate.ToUniversalTime(),
            IconEmoji = req.IconEmoji,
            Notes = req.Notes?.Trim(),
            IsActive = true,
        };

        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();

        var exchangeRates = await _db.ExchangeRates
            .ToDictionaryAsync(r => r.CurrencyCode, r => r.RateToKRW);

        return CreatedAtAction(nameof(GetSubscriptions), ToDto(subscription, exchangeRates));
    }

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
