using NiTorrent.Presentation.Abstractions;

namespace NiTorrent.App.Services;

internal sealed class StartupErrorPresenter(IUiDispatcher dispatcher) : IStartupErrorPresenter
{
    private Window? _window;
    public Task ShowAsync(string message, Func<Task> exit, Func<bool> isStopping) => dispatcher.EnqueueAsync(() =>
    {
        if (isStopping()) return;
        var close = new Button { Content = "Выход" };
        close.Click += (_, _) => _ = exit();
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Не удалось запустить NiTorrent", FontSize = 20 });
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(close);
        _window = new Window { Title = "NiTorrent — ошибка запуска", Content = panel };
        _window.Closed += (_, _) => _ = exit();
        _window.Activate();
    });
    public Task CloseAsync(CancellationToken ct) => dispatcher.EnqueueAsync(() => _window?.Close(), ct);
}
