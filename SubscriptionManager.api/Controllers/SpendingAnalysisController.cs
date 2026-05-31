using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.api.Models;
using SubscriptionManager.api.Services;

namespace SubscriptionManager.api.Controllers;

[ApiController]
[Authorize]
[Route("api/subscriptions")]
public class SpendingAnalysisController : ControllerBase
{
    private readonly ISpendingAnalysisService _spendingAnalysisService;

    public SpendingAnalysisController(ISpendingAnalysisService spendingAnalysisService)
    {
        _spendingAnalysisService = spendingAnalysisService;
    }

    [HttpGet("spending-trends")]
    public async Task<ActionResult<SpendingTrendsDto>> GetSpendingTrends(
        [FromQuery] int months = 12)
    {
        var result = await _spendingAnalysisService.GetSpendingTrendsAsync(
            GetUserId(),
            Math.Clamp(months, 1, 24));

        return Ok(result);
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User id claim not found.");
    }
}