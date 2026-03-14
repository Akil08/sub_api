using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using subscription_api.Data;
using subscription_api.DTOs;
using subscription_api.Models;
using subscription_api.Services;

namespace subscription_api.Controllers;

[ApiController]
[Route("api")]
public class SubscriptionController : ControllerBase
{
    private readonly IRateLimitService _rateLimitService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly AppDbContext _context;

    public SubscriptionController(
        IRateLimitService rateLimitService, 
        ISubscriptionService subscriptionService, 
        AppDbContext context)
    {
        _rateLimitService = rateLimitService;
        _subscriptionService = subscriptionService;
        _context = context;
    }

    [HttpGet("data/user/{userId:int}")]
    public async Task<IActionResult> GetData(int userId)
    {
        // Check if user exists
        var user = await _subscriptionService.GetUserAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        // Check rate limit (Redis)
        var isRateLimited = await _rateLimitService.IsRateLimitedAsync(userId);
        if (isRateLimited)
        {
            return StatusCode(429, new { message = "Too Many Requests" });
        }

        // Check monthly quota
        var subscription = await _subscriptionService.GetSubscriptionAsync(userId);
        if (subscription == null || subscription.UsedThisMonth >= subscription.MonthlyQuota)
        {
            return StatusCode(429, new { message = "Monthly Quota Exceeded" });
        }

        // Increment usage atomically
        var success = await _subscriptionService.IncrementUsageAsync(userId);
        if (!success)
        {
            return StatusCode(429, new { message = "Monthly Quota Exceeded" });
        }

        return Ok(new { data = "Some API response data" });
    }

    [HttpPost("upgrade/user/{userId:int}")]
    public async Task<IActionResult> Upgrade(int userId, [FromBody] UpgradeRequestDto request)
    {
        // Auto-create user if doesn't exist
        var user = await _subscriptionService.GetUserAsync(userId);
        if (user == null)
        {
            user = new User { Id = userId, Email = $"user{userId}@example.com" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        // Validate plan
        if (request.Plan != "Free" && request.Plan != "Pro")
        {
            return BadRequest(new { message = "Invalid plan. Must be 'Free' or 'Pro'." });
        }

        // Upgrade/Downgrade
        await _subscriptionService.UpgradeAsync(userId, request.Plan);

        // Get updated subscription
        var subscription = await _subscriptionService.GetSubscriptionAsync(userId);

        return Ok(new
        {
            success = true,
            message = $"Successfully upgraded to {request.Plan} plan",
            monthlyQuota = subscription?.MonthlyQuota,
            usedThisMonth = 0
        });
    }
}