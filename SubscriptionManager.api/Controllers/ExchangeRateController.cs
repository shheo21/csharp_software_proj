using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.api.Services;

namespace SubscriptionManager.api.Controllers;

[ApiController]
[Route("api/exchangerate")]
public class ExchangeRateController : ControllerBase
{
    private readonly ExchangeRateService _exchangeRateService;

    public ExchangeRateController(ExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rates = await _exchangeRateService.GetAllAsync();
        return Ok(rates.Select(r => new
        {
            r.CurrencyCode,
            r.CurrencyName,
            r.RateToKRW,
            r.UpdatedAt,
        }));
    }

    [HttpGet("{currencyCode}")]
    public async Task<IActionResult> GetOne(string currencyCode)
    {
        var code = currencyCode.Trim().ToUpper();
        var rate = await _exchangeRateService.GetRateAsync(code);
        return Ok(new { currencyCode = code, rateToKRW = rate });
    }

    [HttpPost("refresh")]
    public IActionResult Refresh()
    {
        _exchangeRateService.InvalidateCache();
        return Ok(new { message = "환율 캐시가 초기화되었습니다." });
    }
}
