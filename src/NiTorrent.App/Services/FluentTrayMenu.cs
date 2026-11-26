using H.NotifyIcon.Core;
using H.NotifyIcon.Interop;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace NiTorrent.App.Services;

/// <summary>A transparent tool window hosts the WinUI flyout while the main window is hidden.</summary>
internal sealed partial class FluentTrayMenu : IDisposable
{
    private readonly Window _window;
    private readonly Grid _root;
    private readonly MenuFlyout _flyout;
    private readonly MenuFlyoutItem _speeds;
    private readonly MenuFlyoutItem _pause;
    private readonly MenuFlyoutItem _resume;
    private readonly Action<Exception> _failed;
    private bool _visible;
    private bool _disposed;

    public FluentTrayMenu(Action open, Action pause, Action resume, Action exit, Action<Exception> failed)
    {
        _failed = failed;
        _root = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        _window = new Window { Title = "NiTorrent.TrayMenu", Content = _root };
        var handle = WindowNative.GetWindowHandle(_window);
        // Use tool-window styles instead of IsShownInSwitchers, which can fail in restricted sessions.
        try
        {
            HwndUtilities.SetWindowStyleAsPopupWindow(handle);
            NativeTrayWindow.HideFromTaskbar(handle);
            WindowUtilities.MakeTransparent(handle);
            if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = false;
                presenter.IsAlwaysOnTop = true;
            }
        }
        catch { _window.Close(); throw; }

        _flyout = new MenuFlyout { ShouldConstrainToRootBounds = false };
        AutomationProperties.SetName(_flyout, "Меню NiTorrent");
        _speeds = new MenuFlyoutItem { IsEnabled = false };
        _flyout.Items.Add(_speeds);
        _flyout.Items.Add(new MenuFlyoutSeparator());
        _flyout.Items.Add(Item("Открыть", "\uE8A7", open));
        _pause = Item("Приостановить всё", "\uE769", pause);
        _resume = Item("Продолжить всё", "\uE768", resume);
        _flyout.Items.Add(_pause);
        _flyout.Items.Add(_resume);
        _flyout.Items.Add(new MenuFlyoutSeparator());
        _flyout.Items.Add(Item("Выход", "\uE7E8", exit));
        _flyout.Closed += OnFlyoutClosed;
        _window.Activated += OnActivated;
        _root.Loaded += OnLoaded;
    }

    private MenuFlyoutItem Item(string text, string glyph, Action action)
    {
        var item = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = glyph } };
        item.Click += (_, _) => { Hide(); action(); };
        return item;
    }

    public void Show(System.Drawing.Point anchor, ElementTheme theme, string speeds, bool busy)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Update(speeds, busy);
        _root.RequestedTheme = theme;
        if (_visible) return;
        // H.NotifyIcon's V4 message coordinates contain signed 16-bit screen coordinates.
        var point = new PointInt32(unchecked((short)anchor.X), unchecked((short)anchor.Y));
        var area = DisplayArea.GetFromPoint(point, DisplayAreaFallback.Nearest).WorkArea;
        point.X = Math.Clamp(point.X, area.X, area.X + area.Width - 1);
        point.Y = Math.Clamp(point.Y, area.Y, area.Y + area.Height - 1);
        _window.AppWindow.MoveAndResize(new RectInt32(point.X, point.Y, 1, 1));
        _visible = true;
        _window.AppWindow.Show();
        _window.Activate();
        WindowUtilities.SetForegroundWindow(WindowNative.GetWindowHandle(_window));
        _window.DispatcherQueue.TryEnqueue(OpenFlyout);
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => OpenFlyout();
    private void OpenFlyout()
    {
        if (_disposed || !_visible || _root.XamlRoot is null || _flyout.IsOpen) return;
        try
        {
            _flyout.ShowAt(_root, new FlyoutShowOptions
            {
                Position = new Windows.Foundation.Point(0, 0),
                Placement = FlyoutPlacementMode.Auto,
                ShowMode = FlyoutShowMode.Standard
            });
        }
        catch (Exception ex) { Hide(); _failed(ex); }
    }

    public void Update(string speeds, bool busy)
    {
        _speeds.Text = speeds;
        _pause.IsEnabled = _resume.IsEnabled = !busy;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated) Hide();
    }
    private void OnFlyoutClosed(object? sender, object args) => Hide();

    public void Hide()
    {
        if (_disposed || !_visible) return;
        _visible = false;
        _flyout.Hide();
        _window.AppWindow.Hide();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Hide();
        _disposed = true;
        _flyout.Closed -= OnFlyoutClosed;
        _window.Activated -= OnActivated;
        _root.Loaded -= OnLoaded;
        _window.Close();
    }
}
