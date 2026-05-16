namespace SubscriptionManager.Api.Models;

public class Notification
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }

    public string Type { get; set; } = "RenewalDue";

    // 이 알림이 가리키는 실제 갱신 예정일
    public DateTime RenewalDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public class NotificationDto
{
    public int Id { get; set; }

    public int SubscriptionId { get; set; }
    public string SubscriptionName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? IconEmoji { get; set; }

    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;

    public DateTime RenewalDate { get; set; }
    public int DaysUntilRenewal { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead { get; set; }
}
