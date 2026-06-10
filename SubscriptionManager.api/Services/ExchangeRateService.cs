using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SubscriptionManager.api.Data;
using SubscriptionManager.api.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SubscriptionManager.api.Services;

public class ExchangeRateService
{
    private const string CacheKey = "exchange_rates";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        IMemoryCache cache,
        ILogger<ExchangeRateService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<ExchangeRate>> GetAllAsync()
    {
        if (!_cache.TryGetValue(CacheKey, out List<ExchangeRate>? rates))
        {
            rates = await FetchAndPersistAsync();
            _cache.Set(CacheKey, rates, CacheDuration);
        }
        return rates!.OrderBy(r => r.CurrencyCode);
    }

    public async Task<decimal> GetRateAsync(string currencyCode)
    {
        var code = currencyCode.Trim().ToUpper();
        var all = await GetAllAsync();
        return all.FirstOrDefault(r => r.CurrencyCode == code)?.RateToKRW ?? 1m;
    }

    public void InvalidateCache() => _cache.Remove(CacheKey);

    // API에서 가져와 DB upsert 후 반환. 실패/빈 응답 시 DB 폴백.
    private async Task<List<ExchangeRate>> FetchAndPersistAsync()
    {
        try
        {
            var apiKey = _config["ExchangeRate:ApiKey"]
                ?? throw new InvalidOperationException("ExchangeRate:ApiKey가 설정되지 않았습니다.");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/8.7.1");

            // 영업일 11시 이전이나 비영업일엔 당일 데이터가 없으므로 최대 7일 전까지 재시도
            var kstNow = DateTime.UtcNow.AddHours(9);
            List<EximbankItem>? items = null;

            for (var i = 0; i < 7; i++)
            {
                var date = kstNow.AddDays(-i).ToString("yyyyMMdd");
                var url = $"https://oapi.koreaexim.go.kr/site/program/financial/exchangeJSON?authkey={apiKey}&searchdate={date}&data=AP01";
                var json = await client.GetStringAsync(url);
                var parsed = JsonSerializer.Deserialize<List<EximbankItem>>(json);

                if (parsed != null && parsed.Count > 0 && parsed[0].Result == 1)
                {
                    items = parsed;
                    break;
                }
            }

            if (items == null)
                return await LoadFromDbAsync();

            var now = DateTime.UtcNow;
            foreach (var item in items)
            {
                if (item.Result != 1) continue;

                // "JPY(100)" → isPer100=true, code="JPY"
                var isPer100 = item.CurUnit.Contains("(100)");
                var code = item.CurUnit.Split('(')[0].Trim();

                var rawRate = item.DealBasR?.Replace(",", "");
                if (!decimal.TryParse(rawRate, out var rate)) continue;

                if (isPer100)
                    rate /= 100m;

                var existing = await _db.ExchangeRates.FirstOrDefaultAsync(r => r.CurrencyCode == code);
                if (existing != null)
                {
                    existing.RateToKRW = Math.Round(rate, 4);
                    existing.CurrencyName = item.CurNm ?? existing.CurrencyName;
                    existing.UpdatedAt = now;
                }
                else
                {
                    _db.ExchangeRates.Add(new ExchangeRate
                    {
                        CurrencyCode = code,
                        CurrencyName = item.CurNm ?? code,
                        RateToKRW = Math.Round(rate, 4),
                        UpdatedAt = now,
                    });
                }
            }
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "한국수출입은행 API 호출 실패. DB 폴백 환율을 사용합니다.");
        }

        return await LoadFromDbAsync();
    }

    private Task<List<ExchangeRate>> LoadFromDbAsync() =>
        _db.ExchangeRates.ToListAsync();

    // 한국수출입은행 API 응답 매핑용 내부 레코드
    private sealed record EximbankItem(
        [property: JsonPropertyName("result")] int Result,
        [property: JsonPropertyName("cur_unit")] string CurUnit,
        [property: JsonPropertyName("cur_nm")] string? CurNm,
        [property: JsonPropertyName("deal_bas_r")] string? DealBasR
    );
}
