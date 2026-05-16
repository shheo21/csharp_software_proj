using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SubscriptionManager.api.Models;

namespace SubscriptionManager.api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();    // 구독 테이블
    public DbSet<Notification> Notifications => Set<Notification>();    // 알림 테이블

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.DisplayName).HasMaxLength(100);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Subscription 엔티티 설정
        builder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Currency)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.BillingCycle)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.IsActive });
            entity.HasIndex(e => new { e.UserId, e.NextBillingDate });
        });

        // Notification 엔티티 설정
        builder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Subscription)
                .WithMany()
                .HasForeignKey(e => e.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // 같은 사용자, 구독, 갱신일에 대해 중복 알림 생성 방지
            entity.HasIndex(e => new
            {
                e.UserId,
                e.SubscriptionId,
                e.Type,
                e.RenewalDate
            }).IsUnique();

            entity.HasIndex(e => new
            {
                e.UserId,
                e.ReadAt,
                e.RenewalDate
            });
        });
    }
}
