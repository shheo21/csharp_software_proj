using System.Text.Json;
using Microsoft.JSInterop;

namespace SubscriptionManager.blazor.Services;

public interface ICategoryService
{
    IReadOnlyList<string> Categories { get; }
    event Action? OnChange;
    Task EnsureLoadedAsync(IEnumerable<string>? initialCategories = null);
    Task AddAsync(string name);
    Task RemoveAsync(string name);
    string GetColor(string? category);
    Task SetColorAsync(string name, string color);
    Task ResetColorAsync(string name);
    Task ResetColorsAsync();
}

public class CategoryService : ICategoryService
{
    private const string StorageKey = "subtrack_categories";
    private const string ColorStorageKey = "subtrack_category_colors";
    private const string LegacyCalendarColorStorageKey = "subtrack_calendar_colors";
    private const string FallbackCategory = "기타";

    private static readonly List<string> Defaults =
    [
        "엔터테인먼트", "음악", "창작 도구", "개발 도구",
        "클라우드 스토리지", "생산성", "뉴스/미디어",
        "게임", "교육", "건강/피트니스", "기타"
    ];

    private static readonly Dictionary<string, string> DefaultColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["생산성"] = "#F2C94C",
        ["클라우드"] = "#7C3AED",
        ["AI 서비스"] = "#10B981",
        ["엔터테인먼트"] = "#EC4899",
        ["음악"] = "#F97316",
        ["개발 도구"] = "#38BDF8",
        ["교육"] = "#8B5CF6",
        ["게임"] = "#84CC16",
        ["클라우드 스토리지"] = "#14B8A6",
        ["뉴스/미디어"] = "#06B6D4",
        ["건강/피트니스"] = "#22C55E",
        ["창작 도구"] = "#F59E0B",
        ["기타"] = "#94A3B8",
    };

    private static readonly int[] FallbackHues =
    [
        42, 274, 156, 336, 204, 18, 96, 312,
        184, 246, 126, 354, 224, 72, 144, 292,
        28, 262, 108, 198, 326, 58, 168, 238
    ];

    private readonly ISubscriptionApiService _subscriptionApiService;
    private readonly IJSRuntime _js;
    private List<string> _categories = [];
    private Dictionary<string, string> _categoryColors = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public CategoryService(IJSRuntime js, ISubscriptionApiService subscriptionApiService)
    {
        _js = js;
        _subscriptionApiService = subscriptionApiService;
    }

    public IReadOnlyList<string> Categories => _categories;
    public event Action? OnChange;

    public async Task EnsureLoadedAsync(IEnumerable<string>? initialCategories = null)
    {
        if (_loaded)
        {
            await MergeInitialCategoriesAsync(initialCategories);
            return;
        }

        List<string>? storedCategories = null;

        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (json is not null)
            {
                var stored = JsonSerializer.Deserialize<List<string>>(json);
                if (stored is not null)
                    storedCategories = NormalizeCategories(stored);
            }
        }
        catch { /* localStorage 접근 안할 시 기본값과 구독 목록 카테고리만 사용 */ }

        _categoryColors = await LoadStoredColorsAsync();

        var subscriptionCategories = await LoadSubscriptionCategoriesAsync(initialCategories);
        _categories = MergeCategories(Defaults, storedCategories ?? [], subscriptionCategories);

        if (storedCategories is null || !CategoriesEqual(storedCategories, _categories))
            await PersistAsync();

        _loaded = true;
    }

    public async Task AddAsync(string name)
    {
        await EnsureLoadedAsync();

        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return;
        if (_categories.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) return;

        _categories.Add(trimmed);
        await PersistAsync();
        if (!_categoryColors.ContainsKey(trimmed))
        {
            _categoryColors[trimmed] = GenerateDefaultColor(trimmed);
            await PersistColorsAsync();
        }
        OnChange?.Invoke();
    }

    public async Task RemoveAsync(string name)
    {
        await EnsureLoadedAsync();

        if (!_categories.Remove(name)) return;
        _categoryColors.Remove(name);
        await PersistAsync();
        await PersistColorsAsync();
        OnChange?.Invoke();
    }

    public string GetColor(string? category)
    {
        var name = NormalizeCategoryName(category);
        if (_categoryColors.TryGetValue(name, out var color))
            return color;

        return GenerateDefaultColor(name);
    }

    public async Task SetColorAsync(string name, string color)
    {
        await EnsureLoadedAsync();

        var category = NormalizeCategoryName(name);
        if (!TryNormalizeColor(color, out var normalized))
            return;

        _categoryColors[category] = normalized;
        await PersistColorsAsync();
        OnChange?.Invoke();
    }

    public async Task ResetColorAsync(string name)
    {
        await EnsureLoadedAsync();

        var category = NormalizeCategoryName(name);
        if (!_categoryColors.Remove(category)) return;

        await PersistColorsAsync();
        OnChange?.Invoke();
    }

    public async Task ResetColorsAsync()
    {
        await EnsureLoadedAsync();

        if (_categoryColors.Count == 0) return;

        _categoryColors.Clear();
        await PersistColorsAsync();
        OnChange?.Invoke();
    }

    private async Task<List<string>> LoadSubscriptionCategoriesAsync(
        IEnumerable<string>? initialCategories)
    {
        if (initialCategories is not null)
            return NormalizeCategories(initialCategories);

        try
        {
            var subscriptions = await _subscriptionApiService.GetSubscriptionsAsync();
            return NormalizeCategories(subscriptions.Select(s => s.Category));
        }
        catch
        {
            return [];
        }
    }

    private static List<string> MergeCategories(params IEnumerable<string?>[] categoryGroups)
    {
        var merged = NormalizeCategories(categoryGroups.SelectMany(g => g));
        if (merged.Count == 0)
            merged.Add(FallbackCategory);

        return merged;
    }

    private static List<string> NormalizeCategories(IEnumerable<string?> categories)
    {
        var result = new List<string>();

        foreach (var category in categories)
        {
            var trimmed = category?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (result.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) continue;

            result.Add(trimmed);
        }

        return result;
    }

    private async Task MergeInitialCategoriesAsync(IEnumerable<string>? initialCategories)
    {
        if (initialCategories is null) return;

        var merged = MergeCategories(_categories, NormalizeCategories(initialCategories));
        if (CategoriesEqual(_categories, merged)) return;

        _categories = merged;
        await PersistAsync();
        OnChange?.Invoke();
    }

    private static bool CategoriesEqual(List<string> left, List<string> right)
    {
        if (left.Count != right.Count) return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private async Task PersistAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_categories);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch { /* persistence 실패해도 메모리 상태는 유지 */ }
    }

    private async Task<Dictionary<string, string>> LoadStoredColorsAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await MergeStoredColorsAsync(result, LegacyCalendarColorStorageKey);
        await MergeStoredColorsAsync(result, ColorStorageKey);

        return result;
    }

    private async Task MergeStoredColorsAsync(Dictionary<string, string> target, string key)
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", key);
            if (string.IsNullOrWhiteSpace(json)) return;

            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (stored is null) return;

            foreach (var (category, color) in stored)
            {
                var name = NormalizeCategoryName(category);
                if (!TryNormalizeColor(color, out var normalized)) continue;

                target[name] = normalized;
            }
        }
        catch { /* 색상 저장소가 깨져도 기본 색상으로 동작 */ }
    }

    private async Task PersistColorsAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_categoryColors);
            await _js.InvokeVoidAsync("localStorage.setItem", ColorStorageKey, json);
        }
        catch { /* persistence 실패해도 메모리 상태는 유지 */ }
    }

    private static string NormalizeCategoryName(string? category)
    {
        var trimmed = category?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? FallbackCategory : trimmed;
    }

    private static bool TryNormalizeColor(string? color, out string normalized)
    {
        normalized = string.Empty;
        var value = color?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value[0] != '#') return false;

        if (value.Length == 4 &&
            IsHex(value[1]) && IsHex(value[2]) && IsHex(value[3]))
        {
            normalized = $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}".ToUpperInvariant();
            return true;
        }

        if (value.Length != 7) return false;

        for (var i = 1; i < value.Length; i++)
        {
            if (!IsHex(value[i])) return false;
        }

        normalized = value.ToUpperInvariant();
        return true;
    }

    private static bool IsHex(char ch) =>
        ch is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static string GenerateDefaultColor(string category)
    {
        if (DefaultColors.TryGetValue(category, out var color))
            return color;

        var hash = StableHash(category);
        var hue = FallbackHues[hash % FallbackHues.Length] + (int)((hash >> 8) % 9) - 4;
        var saturation = 72 + (int)((hash >> 12) % 12);
        var lightness = 49 + (int)((hash >> 18) % 8);

        return HslToHex(hue, saturation, lightness);
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in value)
            {
                hash ^= ch;
                hash *= 16777619u;
            }

            return (int)(hash & 0x7fffffff);
        }
    }

    private static string HslToHex(double hue, double saturation, double lightness)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation /= 100d;
        lightness /= 100d;

        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = c * (1 - Math.Abs(hue / 60d % 2 - 1));
        var m = lightness - c / 2;

        var (r1, g1, b1) = hue switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x)
        };

        var r = ToColorByte(r1 + m);
        var g = ToColorByte(g1 + m);
        var b = ToColorByte(b1 + m);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static int ToColorByte(double value) =>
        (int)Math.Round(Math.Clamp(value, 0d, 1d) * 255);
}
