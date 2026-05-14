namespace SubscriptionManager.blazor.Models;

public class SubscriptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "기타";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KRW";
    public string BillingCycle { get; set; } = "MONTHLY";
    public DateTime NextBillingDate { get; set; }
    public string? IconEmoji { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal AmountInKRW { get; set; }
    public decimal MonthlyAmountInKRW { get; set; }
    public int DaysUntilBilling { get; set; }

    public string BillingCycleLabel => BillingCycle == "YEARLY" ? "연간" : "월간";
    public string DaysLabel => DaysUntilBilling switch
    {
        0 => "오늘",
        1 => "D-1",
        < 0 => "결제 완료",
        _ => $"D-{DaysUntilBilling}"
    };
    public string DaysBadgeColor => DaysUntilBilling switch
    {
        <= 3 => "#FF4757",
        <= 7 => "#FFA502",
        _ => "#2ED573"
    };
}

public class CreateSubscriptionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "기타";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KRW";
    public string BillingCycle { get; set; } = "MONTHLY";
    public DateTime NextBillingDate { get; set; } = DateTime.Now.AddMonths(1);
    public string? IconEmoji { get; set; }
    public string? Notes { get; set; }
}

public class UpdateSubscriptionRequest : CreateSubscriptionRequest
{
    public bool IsActive { get; set; } = true;
}

public class DashboardSummary
{
    public decimal TotalMonthlyKRW { get; set; }
    public decimal TotalYearlyKRW { get; set; }
    public int ActiveCount { get; set; }
    public List<SubscriptionDto> UpcomingBilling { get; set; } = new();
    public List<CategorySpend> CategoryBreakdown { get; set; } = new();
}

public class CategorySpend
{
    public string Category { get; set; } = string.Empty;
    public decimal MonthlyAmountKRW { get; set; }
    public int Count { get; set; }
}

public class ExchangeRateSummary
{
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = string.Empty;
    public decimal RateToKRW { get; set; }
    public DateTime UpdatedAt { get; set; }
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
    public string Month { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal TotalKRW { get; set; }
    public List<CategoryMonthlyAmount> ByCategory { get; set; } = new();
}

public class CategoryMonthlyAmount
{
    public string Category { get; set; } = string.Empty;
    public decimal AmountKRW { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public class UserInfo
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
