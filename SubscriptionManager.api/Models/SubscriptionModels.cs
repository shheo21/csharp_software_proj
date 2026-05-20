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

public class CreateSubscriptionRequest
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "구독 이름은 필수입니다.")]
    [System.ComponentModel.DataAnnotations.MaxLength(100, ErrorMessage = "구독 이름은 100자 이하여야 합니다.")]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(50, ErrorMessage = "카테고리는 50자 이하여야 합니다.")]
    public string Category { get; set; } = "기타";

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "금액은 필수입니다.")]
    [System.ComponentModel.DataAnnotations.Range(0.01, double.MaxValue, ErrorMessage = "금액은 0보다 커야 합니다.")]
    public decimal Amount { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(10)]
    public string Currency { get; set; } = "KRW";

    [System.ComponentModel.DataAnnotations.RegularExpression("^(MONTHLY|YEARLY)$", ErrorMessage = "결제 주기는 MONTHLY 또는 YEARLY여야 합니다.")]
    public string BillingCycle { get; set; } = "MONTHLY";

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "다음 결제일은 필수입니다.")]
    public DateTime NextBillingDate { get; set; }

    public string? IconEmoji { get; set; }
    public string? Notes { get; set; }
}

public class UpdateSubscriptionRequest : CreateSubscriptionRequest
{
    public bool IsActive { get; set; } = true;
}

public class SubscriptionQueryParams
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string? Currency { get; set; }
}
