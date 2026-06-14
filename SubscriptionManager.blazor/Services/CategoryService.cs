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
}

public class CategoryService : ICategoryService
{
    private const string StorageKey = "subtrack_categories";
    private const string FallbackCategory = "기타";

    private static readonly List<string> Defaults =
    [
        "엔터테인먼트", "음악", "창작 도구", "개발 도구",
        "클라우드 스토리지", "생산성", "뉴스/미디어",
        "게임", "교육", "건강/피트니스", "기타"
    ];

    private readonly ISubscriptionApiService _subscriptionApiService;
    private readonly IJSRuntime _js;
    private List<string> _categories = [];
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
        if (_loaded) return;

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
        OnChange?.Invoke();
    }

    public async Task RemoveAsync(string name)
    {
        await EnsureLoadedAsync();

        if (!_categories.Remove(name)) return;
        await PersistAsync();
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
}
