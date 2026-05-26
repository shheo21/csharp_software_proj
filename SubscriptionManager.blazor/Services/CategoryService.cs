using System.Text.Json;
using Microsoft.JSInterop;

namespace SubscriptionManager.blazor.Services;

public interface ICategoryService
{
    IReadOnlyList<string> Categories { get; }
    event Action? OnChange;
    Task EnsureLoadedAsync();
    Task AddAsync(string name);
    Task RemoveAsync(string name);
}

public class CategoryService : ICategoryService
{
    private const string StorageKey = "subtrack_categories";

    private static readonly List<string> Defaults =
    [
        "엔터테인먼트", "음악", "창작 도구", "개발 도구",
        "클라우드 스토리지", "생산성", "뉴스/미디어",
        "게임", "교육", "건강/피트니스", "기타"
    ];

    private readonly IJSRuntime _js;
    private List<string> _categories = [];
    private bool _loaded;

    public CategoryService(IJSRuntime js) => _js = js;

    public IReadOnlyList<string> Categories => _categories;
    public event Action? OnChange;

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var stored = JsonSerializer.Deserialize<List<string>>(json);
                if (stored is { Count: > 0 })
                    _categories = stored;
            }
        }
        catch { /* localStorage 미접근 시 기본값 */ }

        if (_categories.Count == 0)
            _categories = new List<string>(Defaults);

        _loaded = true;
    }

    public async Task AddAsync(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return;
        if (_categories.Contains(trimmed)) return;

        _categories.Add(trimmed);
        await PersistAsync();
        OnChange?.Invoke();
    }

    public async Task RemoveAsync(string name)
    {
        if (!_categories.Remove(name)) return;
        await PersistAsync();
        OnChange?.Invoke();
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
