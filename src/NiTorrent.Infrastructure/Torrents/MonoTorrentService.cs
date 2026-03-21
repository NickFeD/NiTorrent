using Microsoft.Extensions.Logging;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class MonoTorrentService : ITorrentService
{
    private readonly ILogger<MonoTorrentService> _logger;
    private readonly IAppStorageService _storage;
    private readonly TorrentCatalogStore _catalogStore;
    private readonly TorrentRuntimeRegistry _runtimeRegistry;
    private readonly TorrentEngineStateStore _engineStateStore;
    private readonly TorrentCommandExecutor _commandExecutor;
    private readonly TorrentAddExecutor _addExecutor;
    private readonly TorrentSourceResolver _sourceResolver;
    private readonly TorrentSettingsApplier _settingsApplier;
    private readonly TorrentQueryService _queryService;
    private readonly BackgroundTaskRunner _backgroundTasks;
    private readonly TorrentEventOrchestrator _eventOrchestrator;
    private readonly TorrentLifecycleExecutor _lifecycleExecutor;
    private readonly TorrentNotifier _notifier;
    private readonly TorrentStartupCoordinator _startupCoordinator;
    private readonly TorrentCommandQueue _commandQueue = new();

    // Single gate to guarantee consistency across:
    // engine lifecycle, runtime registry, and catalog operations.
    private readonly SemaphoreSlim _opGate = new(1, 1);

    private readonly string _cacheDir;

    public MonoTorrentService(
        ILogger<MonoTorrentService> logger,
        IAppStorageService storage,
        TorrentCatalogStore catalogStore,
        TorrentRuntimeRegistry runtimeRegistry,
        TorrentEngineStateStore engineStateStore,
        TorrentCommandExecutor commandExecutor,
        TorrentAddExecutor addExecutor,
        TorrentSourceResolver sourceResolver,
        TorrentSettingsApplier settingsApplier,
        TorrentQueryService queryService,
        BackgroundTaskRunner backgroundTasks,
        TorrentEventOrchestrator eventOrchestrator,
        TorrentLifecycleExecutor lifecycleExecutor,
        TorrentNotifier notifier,
        TorrentStartupCoordinator startupCoordinator)
    {
        _logger = logger;
        _storage = storage;
        _catalogStore = catalogStore;
        _runtimeRegistry = runtimeRegistry;
        _engineStateStore = engineStateStore;
        _commandExecutor = commandExecutor;
        _addExecutor = addExecutor;
        _sourceResolver = sourceResolver;
        _settingsApplier = settingsApplier;
        _queryService = queryService;
        _backgroundTasks = backgroundTasks;
        _eventOrchestrator = eventOrchestrator;
        _lifecycleExecutor = lifecycleExecutor;
        _notifier = notifier;
        _startupCoordinator = startupCoordinator;

        _cacheDir = _storage.GetCachePath(@"Torrents\cache");
        _storage.EnsureDirectory(_cacheDir);
    }

    private ClientEngine Engine
        => _startupCoordinator.Engine ?? throw new InvalidOperationException("Torrent engine is not initialized yet.");

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _backgroundTasks.Run(_eventOrchestrator.PublishCachedAsync(ct), "publish-cached");

        try
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _notifier.NotifyAsync("Ошибка запуска торрент-движка", $"Не удалось запустить торрент-движок.\n\n{ex.Message}").ConfigureAwait(false);
            throw;
        }
    }

    private Task EnsureStartedAsync(CancellationToken ct = default)
        => _startupCoordinator.EnsureStartedAsync(_opGate, _commandQueue, _eventOrchestrator.RaiseLoaded, ct);

    public IReadOnlyList<TorrentSnapshot> GetAll()
        => _queryService.GetAll(_startupCoordinator.Engine is not null);

    public TorrentSnapshot? TryGet(TorrentId id)
        => _queryService.TryGet(id);

    public async Task<TorrentPreview> GetPreviewAsync(TorrentSource source, CancellationToken ct = default)
    {
        var torrent = await _sourceResolver.ResolveAsync(source, EnsureStartedAsync, () => Engine, ct).ConfigureAwait(false);

        var files = torrent.Files
            .Select(f => new TorrentFileEntry(f.Path, f.Length, true))
            .ToList();

        return new TorrentPreview(torrent.Name, torrent.Size, files);
    }

    public Task<TorrentId> AddAsync(AddTorrentRequest request, CancellationToken ct = default)
        => _lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);

            var id = await _addExecutor.AddAsync(
                Engine,
                request,
                (source, token) => _sourceResolver.ResolveAsync(source, EnsureStartedAsync, () => Engine, token),
                _opGate,
                onBackgroundTaskScheduled: null,
                ct).ConfigureAwait(false);

            _backgroundTasks.Run(SaveAsync(CancellationToken.None), "save-engine-state");
            return id;
        }, ct);

    public Task StartAsync(TorrentId id, CancellationToken ct = default)
        => RunCommandWithNotificationAsync(
            async token =>
            {
                await _commandExecutor.StartAsync(
                    id,
                    _startupCoordinator.IsReady,
                    _opGate,
                    _commandQueue,
                    EnsureStartedAsync,
                    PublishTorrentUpdates,
                    _backgroundTasks.Run,
                    token).ConfigureAwait(false);
            },
            "Не удалось запустить торрент",
            "Команда запуска завершилась ошибкой.",
            ct);

    public Task PauseAsync(TorrentId id, CancellationToken ct = default)
        => _lifecycleExecutor.RunAsync(async () =>
        {
            await _commandExecutor.PauseAsync(
                id,
                _startupCoordinator.IsReady,
                _opGate,
                _commandQueue,
                EnsureStartedAsync,
                PublishTorrentUpdates,
                _backgroundTasks.Run,
                ct).ConfigureAwait(false);
        }, ct);

    public Task StopAsync(TorrentId id, CancellationToken ct = default)
        => _lifecycleExecutor.RunAsync(async () =>
        {
            await _commandExecutor.StopAsync(
                id,
                _startupCoordinator.IsReady,
                _opGate,
                _commandQueue,
                EnsureStartedAsync,
                PublishTorrentUpdates,
                _backgroundTasks.Run,
                ct).ConfigureAwait(false);
        }, ct);

    public Task RemoveAsync(TorrentId id, bool deleteDownloadedData, CancellationToken ct = default)
        => RunCommandWithNotificationAsync(
            async token =>
            {
                await _commandExecutor.RemoveAsync(
                    id,
                    deleteDownloadedData,
                    _startupCoordinator.IsReady,
                    _startupCoordinator.Engine,
                    _opGate,
                    _commandQueue,
                    EnsureStartedAsync,
                    PublishTorrentUpdates,
                    _backgroundTasks.Run,
                    token).ConfigureAwait(false);
            },
            "Не удалось удалить торрент",
            "Команда удаления завершилась ошибкой.",
            ct);

    public event Action? Loaded
    {
        add => _eventOrchestrator.Loaded += value;
        remove => _eventOrchestrator.Loaded -= value;
    }

    public event Action<IReadOnlyList<TorrentSnapshot>>? UpdateTorrent
    {
        add => _eventOrchestrator.UpdateTorrent += value;
        remove => _eventOrchestrator.UpdateTorrent -= value;
    }

    public Task PublishTorrentUpdatesAsync(CancellationToken ct = default)
        => _eventOrchestrator.PublishUpdatesAsync(_startupCoordinator.Engine is not null, _opGate, ct);

    public void PublishTorrentUpdates()
        => _eventOrchestrator.PublishUpdatesInBackground(_startupCoordinator.Engine is not null, _opGate);

    public Task SaveAsync(CancellationToken ct = default)
        => _lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(ct).ConfigureAwait(false);
            await _engineStateStore.SaveAsync(Engine, ct).ConfigureAwait(false);
        }, ct);

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Shutting down torrent service");

        return _lifecycleExecutor.RunAsync(async () =>
        {
            if (_startupCoordinator.Engine is null)
                return;

            await _catalogStore.SaveAsync(force: true, ct).ConfigureAwait(false);
            await _engineStateStore.SaveAsync(Engine, ct).ConfigureAwait(false);
        }, ct);
    }


    private Task RunCommandWithNotificationAsync(
        Func<CancellationToken, Task> action,
        string title,
        string messagePrefix,
        CancellationToken ct)
        => _lifecycleExecutor.RunAsync(async () =>
        {
            try
            {
                await action(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await _notifier.NotifyAsync(title, $"{messagePrefix}\n\n{ex.Message}").ConfigureAwait(false);
                throw;
            }
        }, ct);

    public Task ApplySettingsAsync()
        => _lifecycleExecutor.RunAsync(async () =>
        {
            await EnsureStartedAsync(CancellationToken.None).ConfigureAwait(false);
            await _settingsApplier.ApplyAsync(Engine, _cacheDir).ConfigureAwait(false);
        }, CancellationToken.None);

}
