using NiTorrent.Presentation.Abstractions;
using WinUIEx;

public sealed partial class TrayService : ITrayService, IDisposable
{
    private TrayIcon? _tray;

    private string _totalDl = "0 B/s";
    private string _totalUl = "0 B/s";

    // Чтобы обновлять пункт меню со скоростями без пересоздания
    private MenuFlyoutItem? _speedsItem;

    public event Action? OpenRequested;
    public event Func<System.Threading.Tasks.Task>? ExitRequested;

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

        _tray.ContextMenu += (_, e) => ShowContextMenu(e);
    }

    public void SetVisible(bool visible)
    {
        if (_tray != null)
            _tray.IsVisible = visible;
    }
    public void UpdateTotals(string totalDownload, string totalUpload)
    {
        _totalDl = totalDownload;
        _totalUl = totalUpload;

        if (_tray != null)
            _tray.Tooltip = BuildTooltip();

        if (_speedsItem != null)
            _speedsItem.Text = $"↓ {_totalDl}    ↑ {_totalUl}";
    }

    private string BuildTooltip()
        => $"NiTorrent • ↓ {_totalDl} • ↑ {_totalUl}";

    private void ShowContextMenu(TrayIconEventArgs e)
    {
        var flyout = BuildMenuFlyout();
        e.Flyout = flyout;
    }

    private MenuFlyout BuildMenuFlyout()
    {
        var flyout = new MenuFlyout();

        var open = new MenuFlyoutItem { Text = "Открыть" };
        open.Click += (_, __) => OpenRequested?.Invoke();

        _speedsItem = new MenuFlyoutItem
        {
            Text = $"↓ {_totalDl}    ↑ {_totalUl}",
            IsEnabled = false
        };

        var exit = new MenuFlyoutItem { Text = "Выход" };
        exit.Click += (_, __) => ExitRequested?.Invoke();

        flyout.Items.Add(_speedsItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(open);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(exit);

        return flyout;
    }

    public void Dispose()
    {
        if (_tray != null)
        {
            _tray.IsVisible = false;
            _tray.Dispose();
            _tray = null;
        }
    }
}
