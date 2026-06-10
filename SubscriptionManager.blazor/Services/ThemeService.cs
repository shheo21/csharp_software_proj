using Microsoft.JSInterop;

namespace SubscriptionManager.blazor.Services;

public enum ThemeMode { System, Light, Dark }

public interface IThemeService
{
    ThemeMode Mode { get; }
    bool IsDark { get; }
    event Action? OnChange;
    Task InitializeAsync();
    Task SetModeAsync(ThemeMode mode);
}

public class ThemeService : IThemeService, IAsyncDisposable
{
    private const string StorageKey = "subtrack_theme";

    private readonly IJSRuntime _js;
    private DotNetObjectReference<ThemeService>? _selfRef;
    private bool _systemPrefersDark;
    private bool _initialized;

    public ThemeService(IJSRuntime js)
    {
        _js = js;
    }

    public ThemeMode Mode { get; private set; } = ThemeMode.System;

    public bool IsDark => Mode switch
    {
        ThemeMode.Dark => true,
        ThemeMode.Light => false,
        _ => _systemPrefersDark,
    };

    public event Action? OnChange;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        var stored = await SafeReadStoredModeAsync();
        if (stored.HasValue) Mode = stored.Value;

        _selfRef = DotNetObjectReference.Create(this);
        try
        {
            _systemPrefersDark = await _js.InvokeAsync<bool>("subTrackTheme.init", _selfRef);
        }
        catch
        {
            // JS interop 실패 시 dark 기본
            _systemPrefersDark = true;
        }

        await ApplyAsync();
    }

    public async Task SetModeAsync(ThemeMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        try { await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, mode.ToString()); }
        catch { /* localStorage 미접근 시 메모리 상태만 유지 */ }
        await ApplyAsync();
    }

    [JSInvokable]
    public Task OnSystemPreferenceChanged(bool prefersDark)
    {
        _systemPrefersDark = prefersDark;
        if (Mode == ThemeMode.System)
        {
            return ApplyAsync();
        }
        return Task.CompletedTask;
    }

    private async Task ApplyAsync()
    {
        try { await _js.InvokeVoidAsync("subTrackTheme.apply", IsDark ? "dark" : "light"); }
        catch { /* JS 미준비 — 다음 호출에서 재시도 */ }
        OnChange?.Invoke();
    }

    private async Task<ThemeMode?> SafeReadStoredModeAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse<ThemeMode>(raw, out var parsed))
                return parsed;
        }
        catch { /* localStorage 접근 실패 */ }
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_selfRef != null)
        {
            try { await _js.InvokeVoidAsync("subTrackTheme.dispose"); } catch { }
            _selfRef.Dispose();
            _selfRef = null;
        }
    }
}
