using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Torrents.Contracts;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using NativeTrayIcon = H.NotifyIcon.Core.TrayIcon;

namespace NiTorrent.App.Services;

// Core avoids the WinUI wrapper's automatic recovery and automatic Efficiency Mode.
public sealed partial class TrayService(IUiDispatcher ui, ITorrentObservations observations,
    ITorrentsModule bulkCommands, IThemeService themeService, ILogger<TrayService> logger) : ITrayService
{
    private static readonly Guid IconId = new("0c9992d3-3388-4aef-a055-75c8e4773ce6");
    private NativeTrayIcon? _tray;
    private System.Drawing.Icon? _icon;
    private readonly CancellationTokenSource _stopping = new();
    private bool _bulkBusy;
    private FluentTrayMenu? _menu;
    private bool _disposed;
    private IDisposable? _observation;
    private int _observationVersion;
    private TorrentObservationSnapshot? _snapshot;
    public event Action? OpenRequested;
    public event Func<Task>? ExitRequested;
    public event Action? RecoveryRequested;
    public event Action<int>? BulkFailed;

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Hide();
        try
        {
            _icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
            _tray = new NativeTrayIcon(IconId) { Icon = _icon.Handle, ToolTip = BuildTooltip() };
            _tray.MessageWindow.MouseEventReceived += OnMouseEvent;
            _tray.MessageWindow.KeyboardEventReceived += OnKeyboardEvent;
            _tray.MessageWindow.TaskbarCreated += OnTaskbarCreated;
            _tray.Create();
            if (!_tray.IsCreated) throw new InvalidOperationException("Tray registration did not complete.");
            var version = ++_observationVersion;
            _observation = observations.Observe(new(TorrentObservationKind.Summary),
                (snapshot, ct) => OnStatusesChanged(snapshot, version, ct));
            logger.LogInformation("[Tray] Icon registered");
        }
        catch { Hide(); throw; }
    }

    public void Hide()
    {
        _menu?.Dispose();
        _menu = null;
        ++_observationVersion;
        _observation?.Dispose();
        _observation = null;
        var tray = _tray;
        _tray = null;
        if (tray is not null)
        {
            tray.MessageWindow.MouseEventReceived -= OnMouseEvent;
            tray.MessageWindow.KeyboardEventReceived -= OnKeyboardEvent;
            tray.MessageWindow.TaskbarCreated -= OnTaskbarCreated;
            // Create may fail after NIM_ADD but before IsCreated. Delete even in that case.
            try { _ = NativeTrayCleanup.Delete(IconId); }
            catch (Exception ex) { logger.LogWarning(ex, "[Tray] Icon removal failed"); }
            try { tray.Dispose(); }
            catch (Exception ex) { logger.LogWarning(ex, "[Tray] Message window cleanup failed"); }
        }
        _icon?.Dispose();
        _icon = null;
    }

    private void OnTaskbarCreated(object? sender, EventArgs args)
    {
        var tray = _tray;
        // Never destroy a native window from within its own message callback.
        ui.TryEnqueue(() =>
        {
            if (tray is null || tray != _tray || _disposed) return;
            Hide();
            RecoveryRequested?.Invoke();
        });
    }

    private void OnMouseEvent(object? sender, MessageWindow.MouseEventReceivedEventArgs args)
    {
        if (args.MouseEvent == MouseEvent.IconLeftDoubleClick)
            ui.TryEnqueue(() => OpenRequested?.Invoke());
        else if (args.MouseEvent == MouseEvent.IconRightMouseUp)
            ui.TryEnqueue(() => ShowMenuSafely(args.Point));
    }

    private void OnKeyboardEvent(object? sender, MessageWindow.KeyboardEventReceivedEventArgs args)
    {
        if (args.KeyboardEvent == KeyboardEvent.ContextMenu) ui.TryEnqueue(() => ShowMenuSafely(args.Point));
        else if (args.KeyboardEvent == KeyboardEvent.KeySelect) ui.TryEnqueue(() => OpenRequested?.Invoke());
    }

    private string SpeedText()
    {
        var suffix = _snapshot?.Connection == TorrentConnectionState.Disconnected ? " • связь потеряна" : "";
        return $"↓ {SizeFormatter.FormatSpeed(_snapshot?.DownloadSpeed ?? 0)}    ↑ {SizeFormatter.FormatSpeed(_snapshot?.UploadSpeed ?? 0)}{suffix}";
    }
    private string BuildTooltip() => $"NiTorrent • {SpeedText()}";
    private Task OnStatusesChanged(TorrentObservationSnapshot snapshot, int version, CancellationToken ct)
    {
        return ui.EnqueueAsync(() =>
        {
            if (_tray is not { } tray || version != _observationVersion) return;
            _snapshot = snapshot;
            try { tray.UpdateToolTip(BuildTooltip()); }
            catch (Exception ex) { logger.LogWarning(ex, "[Tray] Tooltip update failed"); }
            _menu?.Update(SpeedText(), _bulkBusy);
        }, ct);
    }

    private void ShowMenuSafely(System.Drawing.Point position)
    {
        if (_tray is null || _disposed) return;
        try
        {
            _menu ??= new FluentTrayMenu(() => OpenRequested?.Invoke(),
                () => _ = RunBulkAsync(false), () => _ = RunBulkAsync(true), () => _ = ExitSafelyAsync(), OnMenuFailed);
            _menu.Show(position, themeService.ActualTheme, SpeedText(), _bulkBusy);
        }
        catch (Exception ex) { OnMenuFailed(ex); }
    }

    private void OnMenuFailed(Exception ex)
    {
        logger.LogError(ex, "[Tray] Fluent menu failed");
        _menu?.Dispose();
        _menu = null;
        OpenRequested?.Invoke();
    }

    private async Task RunBulkAsync(bool running)
    {
        if (_bulkBusy || _disposed) return;
        _bulkBusy = true;
        try
        {
            var result = await bulkCommands.SetAllAsync(running, _stopping.Token);
            if (result.Failed.Count > 0) BulkFailed?.Invoke(result.Failed.Count);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Tray] Bulk command failed");
            BulkFailed?.Invoke(-1);
        }
        finally { _bulkBusy = false; logger.LogInformation("[Tray] Bulk command finished"); }
    }

    private async Task ExitSafelyAsync()
    {
        try { if (ExitRequested is { } exit) await exit(); }
        catch (Exception ex) { logger.LogError(ex, "[Tray] Exit failed"); }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopping.Cancel();
        Hide();
        _stopping.Dispose();
    }
}
