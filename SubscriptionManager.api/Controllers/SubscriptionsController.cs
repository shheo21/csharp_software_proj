using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.api.Models;
using SubscriptionManager.api.Services;

namespace SubscriptionManager.api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly SubscriptionService _subscriptionService;

    public SubscriptionsController(SubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSubscriptions([FromQuery] SubscriptionQueryParams query)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var result = await _subscriptionService.GetSubscriptionsAsync(userId, query);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubscription([FromBody] CreateSubscriptionRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (!await _subscriptionService.IsCurrencySupportedAsync(req.Currency))
            return BadRequest(new { message = $"지원하지 않는 통화 코드입니다: {req.Currency.Trim().ToUpper()}" });

        var result = await _subscriptionService.CreateSubscriptionAsync(userId, req);
        return CreatedAtAction(nameof(GetSubscriptions), result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSubscription(int id, [FromBody] UpdateSubscriptionRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (!await _subscriptionService.IsCurrencySupportedAsync(req.Currency))
            return BadRequest(new { message = $"지원하지 않는 통화 코드입니다: {req.Currency.Trim().ToUpper()}" });

        var result = await _subscriptionService.UpdateSubscriptionAsync(userId, id, req);
        if (result == null)
            return NotFound(new { message = "구독을 찾을 수 없습니다." });

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubscription(int id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var deleted = await _subscriptionService.DeleteSubscriptionAsync(userId, id);
        if (!deleted)
            return NotFound(new { message = "구독을 찾을 수 없습니다." });

        return NoContent();
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
