using Microsoft.EntityFrameworkCore;
using subscription_api.Data;
using subscription_api.Models;
using subscription_api.Services;
using Xunit;

namespace subscription_api.Tests.Services;

public class SubscriptionServiceTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    #region Test 1: UpgradeAsync - Free to Pro

    [Fact]
    public async Task UpgradeAsync_WhenUpgradingToPro_SetsCorrectQuotaAndEndDate()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        
        // Add test user
        context.Users.Add(new User { Id = 1, Email = "test@example.com" });
        
        // Add existing Free subscription
        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = "Free",
            MonthlyQuota = 1000,
            UsedThisMonth = 500,
            SubscriptionEndDate = DateTime.UtcNow.AddDays(-10)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionService(context);

        // Act
        await service.UpgradeAsync(1, "Pro");

        // Assert
        var subscription = await context.Subscriptions.FirstOrDefaultAsync(s => s.UserId == 1);
        
        Assert.NotNull(subscription);
        Assert.Equal("Pro", subscription.Plan);
        Assert.Equal(10000, subscription.MonthlyQuota);
        Assert.Equal(0, subscription.UsedThisMonth); // Reset on upgrade
        Assert.NotNull(subscription.SubscriptionEndDate);
        Assert.True(subscription.SubscriptionEndDate > DateTime.UtcNow);
    }

    #endregion

    #region Test 2: IncrementUsageAsync - Quota Check

    [Fact]
    public async Task IncrementUsageAsync_WhenQuotaAvailable_IncrementsUsage()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        
        // Add test subscription with quota available
        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = "Pro",
            MonthlyQuota = 10000,
            UsedThisMonth = 100, // Has 9900 remaining
            SubscriptionEndDate = DateTime.UtcNow.AddMonths(1)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionService(context);

        // Act
        var result = await service.IncrementUsageAsync(1);

        // Assert
        Assert.True(result); // Should succeed
        
        var subscription = await context.Subscriptions.FirstOrDefaultAsync(s => s.UserId == 1);
        Assert.Equal(101, subscription.UsedThisMonth); // Incremented by 1
    }

    [Fact]
    public async Task IncrementUsageAsync_WhenQuotaExceeded_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        
        // Add test subscription with NO quota remaining
        context.Subscriptions.Add(new Subscription
        {
            UserId = 1,
            Plan = "Free",
            MonthlyQuota = 1000,
            UsedThisMonth = 1000, // No quota left
            SubscriptionEndDate = DateTime.UtcNow.AddMonths(1)
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionService(context);

        // Act
        var result = await service.IncrementUsageAsync(1);

        // Assert
        Assert.False(result); // Should fail - quota exceeded
    }

    #endregion
}