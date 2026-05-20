namespace SubscriptionManager.api.Models;

public class Subscription
{
    public int Id { get; set; }

    // 현재 repo는 Identity 기반이므로 string UserId 사용
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "기타";

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "KRW";

    // "MONTHLY", "YEARLY"
    public string BillingCycle { get; set; } = "MONTHLY";

    public DateTime NextBillingDate { get; set; }

    public string? IconEmoji { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}