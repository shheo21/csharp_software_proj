namespace SubscriptionManager.api.Models;

public class DashboardSummary
{
    public decimal TotalMonthlyKRW { get; set; }
    public decimal TotalYearlyKRW { get; set; }

    public int ActiveCount { get; set; }
    public int UpcomingBillingCount { get; set; }

    public List<SubscriptionDto> UpcomingBilling { get; set; } = new();
    public List<CategorySpend> CategoryBreakdown { get; set; } = new();
}

public class CategorySpend
{
    public string Category { get; set; } = string.Empty;
    public decimal MonthlyAmountKRW { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class SpendingTrendsDto
{
    public List<MonthlyTrend> MonthlyTrends { get; set; } = new();
    public List<CategorySpend> CategoryBreakdown { get; set; } = new();

    public decimal TotalMonthlyKRW { get; set; }
    public decimal TotalYearlyKRW { get; set; }

    public int ActiveCount { get; set; }
    public string TopCategory { get; set; } = string.Empty;
    public decimal AverageMonthlyKRW { get; set; }
}

public class MonthlyTrend
{
    public string Month { get; set; } = string.Empty; // yyyy-MM
    public string Label { get; set; } = string.Empty; // yy년 M월
    public decimal TotalKRW { get; set; }

    public List<CategoryMonthlyAmount> ByCategory { get; set; } = new();
}

public class CategoryMonthlyAmount
{
    public string Category { get; set; } = string.Empty;
    public decimal AmountKRW { get; set; }
}