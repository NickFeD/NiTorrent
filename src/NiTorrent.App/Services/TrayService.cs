using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Torrents;
using NiTorrent.Presentation.Abstractions;
using WinUIEx;

public sealed partial class TrayService : ITrayService, IDisposable
{
    private readonly ITorrentReadModelFeed _torrentReadModelFeed;
    private readonly ITorrentSpeedSummaryService _torrentSpeedSummaryService;
    private readonly IUiDispatcher _ui;

    private TrayIcon? _tray;
    private MenuFlyoutItem? _speedsItem;

    private string _lastDl = "0 B/s";
    private string _lastUl = "0 B/s";

    public event Action? OpenRequested;
    public event Func<Task>? ExitRequested;

    public TrayService(
        ITorrentReadModelFeed torrentReadModelFeed,
        ITorrentSpeedSummaryService torrentSpeedSummaryService,
        IUiDispatcher ui)
    {
        _torrentReadModelFeed = torrentReadModelFeed;
        _torrentSpeedSummaryService = torrentSpeedSummaryService;
        _ui = ui;
    }

    public void Initialize()
    {
        if (_tray != null)
            return;

        _tray = new TrayIcon(
            trayiconId: 1,
            iconPath: "Assets\\AppIcon.ico",
            tooltip: BuildTooltip())
        {
            IsVisible = false
        };

        _tray.Selected += (_, __) => OpenRequested?.Invoke();
        _tray.ContextMenu += (_, e) => e.Flyout = BuildMenuFlyout();

        _torrentReadModelFeed.Updated += OnTorrentsUpdated;
        OnTorrentsUpdated(_torrentReadModelFeed.GetAll());
    }

    public void SetVisible(bool visible)
    {
        if (_tray != null)
            _tray.IsVisible = visible;
    }

    private void OnTorrentsUpdated(IReadOnlyList<TorrentSnapshot> snapshots)
    {
        var summary = _torrentSpeedSummaryService.Build(snapshots);
        _lastDl = summary.DownloadText;
        _lastUl = summary.UploadText;
        _ = _ui.EnqueueAsync(ApplyUi);
    }

    private void ApplyUi()
    {
        if (_tray != null)
            _tray.Tooltip = BuildTooltip();

        if (_speedsItem != null)
            _speedsItem.Text = $"↓ {_lastDl}    ↑ {_lastUl}";
    }

    private string BuildTooltip()
        => $"NiTorrent • ↓ {_lastDl} • ↑ {_lastUl}";

    private MenuFlyout BuildMenuFlyout()
    {
        var flyout = new MenuFlyout();

        _speedsItem = new MenuFlyoutItem
        {
            Text = $"↓ {_lastDl}    ↑ {_lastUl}",
            IsEnabled = false
        };

        var open = new MenuFlyoutItem { Text = "Открыть" };
        open.Click += (_, __) => OpenRequested?.Invoke();

        var exit = new MenuFlyoutItem { Text = "Выход" };
        exit.Click += async (_, __) =>
        {
            if (ExitRequested != null)
                await ExitRequested();
        };

        flyout.Items.Add(_speedsItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(open);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(exit);

        return flyout;
    }

    public void Dispose()
    {
        _torrentReadModelFeed.Updated -= OnTorrentsUpdated;

        if (_tray != null)
        {
            _tray.IsVisible = false;
            _tray.Dispose();
            _tray = null;
        }
    }
}
