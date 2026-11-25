using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using NiTorrent.Application;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Presentation.Abstractions;
using System.Diagnostics;

namespace NiTorrent.App.Services.AppLifecycle;

/// <summary>Windows activation/window bridge. The host owns the independent core.</summary>
public sealed class NiTorrentApplication(IHost host, IServiceProvider services, Action exit) : INiTorrentApplication
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _startup = new();
    private Task? _startupTask;
    private int _shutdownStarted;
    private Window? _startupErrorWindow;
    private T Get<T>() where T : class => services.GetService<T>()
        ?? throw new InvalidOperationException($"Missing service: {typeof(T)}");

    public Task StartAsync(AppActivationArguments args, CancellationToken ct)
    {
        Get<IActivationQueue>().Enqueue(args);
        lock (_gate)
        {
            if (Volatile.Read(ref _shutdownStarted) != 0) return Task.CompletedTask;
            _startupTask ??= StartCoreAsync(ct);
            return _startupTask;
        }
    }

    private async Task StartCoreAsync(CancellationToken ct)
    {
        Get<UiDispatcherHolder>().Initialize(DispatcherQueue.GetForCurrentThread());
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _startup.Token);
        var token = linked.Token;
        var clock = Stopwatch.StartNew();
        var logger = Get<ILogger<NiTorrentApplication>>();
        try
        {
            await host.StartAsync(token).ConfigureAwait(false);
            logger.LogInformation("[Lifecycle] Core host started in {ElapsedMs} ms", clock.ElapsedMilliseconds);
            token.ThrowIfCancellationRequested();
            // Optional consumer: removing these shell calls does not change core startup.
            await Get<IAppShellLifecycle>().StartAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            await Get<MainWindowLifecycle>().StartTrayAsync(token).ConfigureAwait(false);
            await Get<IActivationQueue>().DrainAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Application startup failed");
            await Get<IUiDispatcher>().EnqueueAsync(() =>
            {
                if (Volatile.Read(ref _shutdownStarted) != 0) return;
                var close = new Button { Content = "Выход" };
                close.Click += (_, _) => _ = ShutdownAsync(AppShutdownReason.UserRequested, CancellationToken.None);
                var panel = new StackPanel { Spacing = 16, Margin = new Thickness(24) };
                panel.Children.Add(new TextBlock { Text = "Не удалось запустить NiTorrent", FontSize = 20 });
                panel.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap });
                panel.Children.Add(close);
                _startupErrorWindow = new Window { Title = "NiTorrent — ошибка запуска", Content = panel };
                _startupErrorWindow.Closed += (_, _) => _ = ShutdownAsync(AppShutdownReason.UserRequested, CancellationToken.None);
                _startupErrorWindow.Activate();
            }).ConfigureAwait(false);
        }
    }

    public async Task HandleActivationAsync(AppActivationArguments args, CancellationToken ct)
    {
        if (Volatile.Read(ref _shutdownStarted) != 0) return;
        var queue = Get<IActivationQueue>();
        queue.Enqueue(args);
        Task? startup;
        lock (_gate) startup = _startupTask;
        if (startup is not null) await startup.ConfigureAwait(false);
        if (Volatile.Read(ref _shutdownStarted) == 0) await queue.DrainAsync(ct).ConfigureAwait(false);
    }

    public async Task ShutdownAsync(AppShutdownReason reason, CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0) return;
        var logger = Get<ILogger<NiTorrentApplication>>();
        var dispatcher = Get<IUiDispatcher>();
        var clock = Stopwatch.StartNew();
        Get<IActivationQueue>().StopAccepting();
        Get<ITorrentSession>().RequestStop();
        _startup.Cancel();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var stopped = false;
        try
        {
            // Only wait for the cancelled host/window startup, never for collection restoration.
            Task? startup;
            lock (_gate) startup = _startupTask;
            if (startup is not null) await startup.WaitAsync(deadline.Token).ConfigureAwait(false);
            await Get<IAppShellLifecycle>().CloseAsync().WaitAsync(deadline.Token).ConfigureAwait(false);
            await dispatcher.EnqueueAsync(() => _startupErrorWindow?.Close(), deadline.Token).ConfigureAwait(false);
            await host.StopAsync(deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            stopped = true;
        }
        catch (Exception ex) { logger.LogError(ex, "Shutdown was incomplete; clean marker is not guaranteed"); }
        finally
        {
            logger.LogInformation("[Lifecycle] Shutdown finished in {ElapsedMs} ms; stopped={Stopped}", clock.ElapsedMilliseconds, stopped);
            if (stopped) await dispatcher.EnqueueAsync(host.Dispose).ConfigureAwait(false);
            await dispatcher.EnqueueAsync(exit).ConfigureAwait(false);
        }
    }
}
