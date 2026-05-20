namespace SubscriptionManager.api.Models;

public class Subscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "기타";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KRW";
    public string BillingCycle { get; set; } = "MONTHLY";
    public DateTime NextBillingDate { get; set; }
    public string? IconEmoji { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}

public class ExchangeRate
{
    public int Id { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencyName { get; set; } = string.Empty;
    public decimal RateToKRW { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SubscriptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = string.Empty;
    public DateTime NextBillingDate { get; set; }
    public string? IconEmoji { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public decimal AmountInKRW { get; set; }
    public decimal MonthlyAmountInKRW { get; set; }
    public int DaysUntilBilling { get; set; }
}

public class SubscriptionQueryParams
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? Currency { get; set; }
}
