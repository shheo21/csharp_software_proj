using Microsoft.EntityFrameworkCore;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Services;

public interface ISpendingAnalysisService
{
    Task<SpendingTrendsDto> GetSpendingTrendsAsync(string userId, int months = 12);
}

public class SpendingAnalysisService : ISpendingAnalysisService
{
    private readonly AppDbContext _db;
    private readonly ISubscriptionCalculationService _calculationService;

    public SpendingAnalysisService(
        AppDbContext db,
        ISubscriptionCalculationService calculationService)
    {
        _db = db;
        _calculationService = calculationService;
    }

    public async Task<SpendingTrendsDto> GetSpendingTrendsAsync(
        string userId,
        int months = 12)
    {
        months = Math.Clamp(months, 1, 24);

        var subscriptions = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        var exchangeRates = await _calculationService.LoadExchangeRatesAsync();

        var items = subscriptions
            .Select(s => new
            {
                Subscription = s,
                Dto = _calculationService.ToDto(s, exchangeRates)
            })
            .ToList();

        var now = DateTime.UtcNow;
        var currentMonth = new DateTime(now.Year, now.Month, 1);

        var trends = new List<MonthlyTrend>();

        for (int i = months - 1; i >= 0; i--)
        {
            var month = currentMonth.AddMonths(-i);
            var monthKey = month.ToString("yyyy-MM");
            var label = month.ToString("yy년 M월");

            var byCategory = new Dictionary<string, decimal>();

            foreach (var item in items)
            {
                var subscription = item.Subscription;
                var dto = item.Dto;

                var createdMonth = new DateTime(
                    subscription.CreatedAt.Year,
                    subscription.CreatedAt.Month,
                    1);

                // 구독이 생성되기 전 월은 정보가 없는 과거이므로 0 처리
                if (month < createdMonth)
                {
                    continue;
                }

                var billingCycle = dto.BillingCycle.ToUpperInvariant();
                decimal contribution = 0;

                if (billingCycle == "MONTHLY")
                {
                    contribution = dto.MonthlyAmountInKRW;
                }
                else if (billingCycle == "YEARLY")
                {
                    var billingMonth = new DateTime(
                        dto.NextBillingDate.Year,
                        dto.NextBillingDate.Month,
                        1);

                    var diff = (month.Year - billingMonth.Year) * 12
                               + (month.Month - billingMonth.Month);

                    if (diff % 12 == 0)
                    {
                        contribution = dto.AmountInKRW;
                    }
                }
                else
                {
                    contribution = dto.MonthlyAmountInKRW;
                }

                if (contribution > 0)
                {
                    byCategory.TryAdd(dto.Category, 0);
                    byCategory[dto.Category] += contribution;
                }
            }

            trends.Add(new MonthlyTrend
            {
                Month = monthKey,
                Label = label,
                TotalKRW = Math.Round(byCategory.Values.Sum(), 0),
                ByCategory = byCategory
                    .Select(kv => new CategoryMonthlyAmount
                    {
                        Category = kv.Key,
                        AmountKRW = Math.Round(kv.Value, 0)
                    })
                    .OrderByDescending(x => x.AmountKRW)
                    .ToList()
            });
        }

        var dtos = items.Select(i => i.Dto).ToList();
        var totalMonthly = dtos.Sum(d => d.MonthlyAmountInKRW);

        var categoryBreakdown = dtos
            .GroupBy(d => d.Category)
            .Select(g =>
            {
                var amount = g.Sum(d => d.MonthlyAmountInKRW);

                return new CategorySpend
                {
                    Category = g.Key,
                    MonthlyAmountKRW = amount,
                    Count = g.Count(),
                    Percentage = totalMonthly == 0
                        ? 0
                        : Math.Round(amount / totalMonthly * 100m, 1)
                };
            })
            .OrderByDescending(c => c.MonthlyAmountKRW)
            .ToList();

        var averageMonthly = trends.Count == 0
            ? 0
            : Math.Round(trends.Average(t => t.TotalKRW), 0);

        return new SpendingTrendsDto
        {
            MonthlyTrends = trends,
            CategoryBreakdown = categoryBreakdown,
            TotalMonthlyKRW = totalMonthly,
            TotalYearlyKRW = totalMonthly * 12m,
            ActiveCount = dtos.Count,
            TopCategory = categoryBreakdown.FirstOrDefault()?.Category ?? "없음",
            AverageMonthlyKRW = averageMonthly
        };
    }
}