using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.UseCase;
using NiTorrent.Presentation;
using NiTorrent.Presentation.Abstractions;
using NativeTrayIcon = H.NotifyIcon.Core.TrayIcon;

namespace NiTorrent.App.Services;

// Core avoids the WinUI wrapper's automatic recovery and automatic Efficiency Mode.
public sealed partial class TrayService(IUiDispatcher ui, ITorrentRuntimeStateSource statuses,
    SetAllTorrentStatesUseCase bulkCommands, ILogger<TrayService> logger) : ITrayService
{
    private static readonly Guid IconId = new("0c9992d3-3388-4aef-a055-75c8e4773ce6");
    private NativeTrayIcon? _tray;
    private System.Drawing.Icon? _icon;
    private readonly CancellationTokenSource _stopping = new();
    private bool _bulkBusy;
    private bool _menuOpen;
    private bool _disposed;
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
            statuses.Subscribe(OnStatusesChanged);
            logger.LogInformation("[Tray] Icon registered");
        }
        catch { Hide(); throw; }
    }

    public void Hide()
    {
        _ = statuses.UnsubscribeAsync(OnStatusesChanged);
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
        var snapshot = statuses.GetSnapshot().Values;
        return $"↓ {SizeFormatter.FormatSpeed(snapshot.Sum(s => s.DownloadSpeed))}    ↑ {SizeFormatter.FormatSpeed(snapshot.Sum(s => s.UploadSpeed))}";
    }
    private string BuildTooltip() => $"NiTorrent • {SpeedText()}";
    private Task OnStatusesChanged(TorrentRuntimeStateChangedEventArgs args)
    {
        ui.TryEnqueue(() =>
        {
            if (_tray is not { } tray) return;
            try { tray.UpdateToolTip(BuildTooltip()); }
            catch (Exception ex) { logger.LogWarning(ex, "[Tray] Tooltip update failed"); }
        });
        return Task.CompletedTask;
    }

    private void ShowMenuSafely(System.Drawing.Point position)
    {
        if (_tray is not { } tray || _disposed || _menuOpen) return;
        _menuOpen = true;
        logger.LogInformation("[Tray] Menu opened; bulkBusy={BulkBusy}", _bulkBusy);
        try
        {
            var menu = new PopupMenu();
            menu.Items.Add(new PopupMenuItem { Text = SpeedText(), Enabled = false });
            menu.Items.Add(new PopupMenuSeparator());
            menu.Items.Add(new PopupMenuItem("Открыть", (_, _) => OpenRequested?.Invoke()));
            menu.Items.Add(new PopupMenuItem("Приостановить всё", (_, _) => _ = RunBulkAsync(false)) { Enabled = !_bulkBusy });
            menu.Items.Add(new PopupMenuItem("Продолжить всё", (_, _) => _ = RunBulkAsync(true)) { Enabled = !_bulkBusy });
            menu.Items.Add(new PopupMenuSeparator());
            menu.Items.Add(new PopupMenuItem("Выход", (_, _) => _ = ExitSafelyAsync()));
            WindowUtilities.SetForegroundWindow(tray.WindowHandle);
            menu.Show(tray.WindowHandle, position.X, position.Y);
        }
        catch (Exception ex) { logger.LogError(ex, "[Tray] Menu failed"); }
        finally { _menuOpen = false; logger.LogInformation("[Tray] Menu closed"); }
    }

    private async Task RunBulkAsync(bool running)
    {
        if (_bulkBusy || _disposed) return;
        _bulkBusy = true;
        try
        {
            var result = await bulkCommands.ExecuteAsync(running, _stopping.Token);
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
