using Microsoft.EntityFrameworkCore;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Services;

public interface IDashboardService
{
    Task<DashboardSummary> GetDashboardSummaryAsync(string userId);
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly ISubscriptionCalculationService _calculationService;

    public DashboardService(
        AppDbContext db,
        ISubscriptionCalculationService calculationService)
    {
        _db = db;
        _calculationService = calculationService;
    }

    public async Task<DashboardSummary> GetDashboardSummaryAsync(string userId)
    {
        var subscriptions = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        var exchangeRates = await _calculationService.LoadExchangeRatesAsync();

        var dtos = subscriptions
            .Select(s => _calculationService.ToDto(s, exchangeRates))
            .ToList();

        var totalMonthly = dtos.Sum(d => d.MonthlyAmountInKRW);

        var upcomingBilling = dtos
            .OrderBy(d => d.DaysUntilBilling)
            .Take(10)
            .ToList();

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

        return new DashboardSummary
        {
            TotalMonthlyKRW = totalMonthly,
            TotalYearlyKRW = totalMonthly * 12m,
            ActiveCount = dtos.Count,
            UpcomingBillingCount = dtos.Count(d => d.DaysUntilBilling <= 7),
            UpcomingBilling = upcomingBilling,
            CategoryBreakdown = categoryBreakdown
        };
    }
}