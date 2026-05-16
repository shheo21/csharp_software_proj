using Microsoft.EntityFrameworkCore;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Services;

public interface INotificationService
{
    Task GenerateRenewalDueNotificationsAsync(string userId);
    Task<List<NotificationDto>> GetNotificationsAsync(string userId, bool unreadOnly = false);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(string userId, int notificationId);
    Task MarkAllAsReadAsync(string userId);
}

public class NotificationService : INotificationService
{
    private const int RenewalDueThresholdDays = 7;

    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task GenerateRenewalDueNotificationsAsync(string userId)
    {
        var today = DateTime.UtcNow.Date;
        var until = today.AddDays(RenewalDueThresholdDays);

        var subscriptions = await _db.Subscriptions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var subscription in subscriptions)
        {
            var renewalDate = NormalizeNextBillingDate(
                subscription.NextBillingDate,
                subscription.BillingCycle,
                today);

            var daysUntilRenewal = (renewalDate.Date - today).Days;

            if (daysUntilRenewal < 0 || daysUntilRenewal > RenewalDueThresholdDays)
            {
                continue;
            }

            var exists = await _db.Notifications.AnyAsync(n =>
                n.UserId == userId &&
                n.SubscriptionId == subscription.Id &&
                n.Type == "RenewalDue" &&
                n.RenewalDate == renewalDate.Date);

            if (exists)
            {
                continue;
            }

            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                SubscriptionId = subscription.Id,
                Type = "RenewalDue",
                RenewalDate = renewalDate.Date,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(
        string userId,
        bool unreadOnly = false)
    {
        await GenerateRenewalDueNotificationsAsync(userId);

        var today = DateTime.UtcNow.Date;
        var until = today.AddDays(RenewalDueThresholdDays);

        var query = _db.Notifications
            .AsNoTracking()
            .Include(n => n.Subscription)
            .Where(n =>
                n.UserId == userId &&
                n.Type == "RenewalDue" &&
                n.RenewalDate >= today &&
                n.RenewalDate <= until);

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var notifications = await query
            .OrderBy(n => n.RenewalDate)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();

        return notifications.Select(ToDto).ToList();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        await GenerateRenewalDueNotificationsAsync(userId);

        var today = DateTime.UtcNow.Date;
        var until = today.AddDays(RenewalDueThresholdDays);

        return await _db.Notifications
            .CountAsync(n =>
                n.UserId == userId &&
                n.Type == "RenewalDue" &&
                n.ReadAt == null &&
                n.RenewalDate >= today &&
                n.RenewalDate <= until);
    }

    public async Task MarkAsReadAsync(string userId, int notificationId)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
        {
            return;
        }

        notification.ReadAt ??= DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var today = DateTime.UtcNow.Date;
        var until = today.AddDays(RenewalDueThresholdDays);

        var notifications = await _db.Notifications
            .Where(n =>
                n.UserId == userId &&
                n.Type == "RenewalDue" &&
                n.ReadAt == null &&
                n.RenewalDate >= today &&
                n.RenewalDate <= until)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.ReadAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    private static NotificationDto ToDto(Notification notification)
    {
        var today = DateTime.UtcNow.Date;
        var daysUntilRenewal = (notification.RenewalDate.Date - today).Days;

        var subscriptionName = notification.Subscription?.Name ?? "알 수 없는 구독";
        var category = notification.Subscription?.Category ?? "기타";

        var title = daysUntilRenewal == 0
            ? "오늘 갱신 예정"
            : "구독 갱신 예정";

        var message = daysUntilRenewal == 0
            ? $"{subscriptionName} 구독이 오늘 갱신됩니다."
            : $"{subscriptionName} 구독 갱신일까지 {daysUntilRenewal}일 남았습니다.";

        var severity = daysUntilRenewal switch
        {
            <= 0 => "Danger",
            <= 3 => "Warning",
            _ => "Info"
        };

        return new NotificationDto
        {
            Id = notification.Id,
            SubscriptionId = notification.SubscriptionId,
            SubscriptionName = subscriptionName,
            Category = category,
            IconEmoji = notification.Subscription?.IconEmoji,
            Type = notification.Type,
            Title = title,
            Message = message,
            Severity = severity,
            RenewalDate = notification.RenewalDate,
            DaysUntilRenewal = daysUntilRenewal,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            IsRead = notification.ReadAt != null
        };
    }

    private static DateTime NormalizeNextBillingDate(
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