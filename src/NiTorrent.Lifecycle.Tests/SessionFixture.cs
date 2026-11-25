using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Application.Torrents;
using NiTorrent.Application.Torrents.Abstract;
using NiTorrent.Application.Torrents.DTo;
using NiTorrent.Application.Torrents.Enum;
using NiTorrent.Domain.Torrents;
using NiTorrent.Infrastructure.DI;
using NiTorrent.Infrastructure.Torrents;

namespace NiTorrent.Lifecycle.Tests;

internal sealed class TemporaryStorage : IAppStorageService, IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "NiTorrent-lifecycle-" + Guid.NewGuid().ToString("N"));
    public string GetLocalPath(string relative) => Path.Combine(Root, relative);
    public string GetCachePath(string relative) => Path.Combine(Root, "Cache", relative);
    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);
    public void EnsureParentDirectory(string path) => Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
}

internal sealed class MemorySettings : ISettingsRepository
{
    private AppSettings _settings;
    public MemorySettings(AppSettings? initial = null) => _settings = initial ?? new();
    public Task<AppSettings> GetAsync(CancellationToken ct) => Task.FromResult(_settings);
    public Task SaveAsync(AppSettings settings, CancellationToken ct) { _settings = settings; return Task.CompletedTask; }
    public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;
}

internal sealed class ShutdownMarker : IShutdownStateService
{
    public bool Clean { get; private set; }
    public Task<PreviousShutdownState> ReadPreviousStateAsync(CancellationToken ct) => Task.FromResult(PreviousShutdownState.Unknown);
    public Task MarkStartedAsync(CancellationToken ct) { Clean = false; return Task.CompletedTask; }
    public Task MarkCleanShutdownAsync(CancellationToken ct) { Clean = true; return Task.CompletedTask; }
}

internal sealed class TestEngine : ITorrentEngineLifecycle
{
    public int Attempts;
    public int Failures;
    public bool Stopped;
    public bool FailStop;
    public Func<CancellationToken, Task>? Starting;
    public async Task StartAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref Attempts);
        if (Starting is not null) await Starting(ct);
        if (Attempts <= Failures) throw new IOException("Simulated engine startup failure");
    }
    public Task StopAcceptingWorkAsync(CancellationToken ct) => Task.CompletedTask;
    public Task StopAsync(CancellationToken ct)
    {
        Stopped = true;
        return FailStop ? Task.FromException(new IOException("Simulated stop failure")) : Task.CompletedTask;
    }
    public void DisposeRuntime() { }
}

internal sealed class TestRuntime : ITorrentRuntimeGateway
{
    public ConcurrentDictionary<Guid, bool> Managers { get; } = new();
    public ConcurrentQueue<(string Operation, Guid Id)> Calls { get; } = new();
    public Func<Guid, CancellationToken, Task>? Adding;
    public Func<Guid, bool>? FailSelection;
    public Task<bool> ExistsByIdAsync(Guid id) => Task.FromResult(Managers.ContainsKey(id));
    public async Task AddAsync(Guid id, TorrentSource source, string path, CancellationToken ct)
    {
        Calls.Enqueue(("Add", id));
        if (Adding is not null) await Adding(id, ct);
        ct.ThrowIfCancellationRequested();
        if (!Managers.TryAdd(id, false)) throw new InvalidOperationException("Duplicate manager");
    }
    public Task UpdateFileSelectionAsync(Guid id, List<TorrentFileEntry> files, CancellationToken ct)
    {
        Calls.Enqueue(("Select", id));
        if (FailSelection?.Invoke(id) == true) throw new IOException("Invalid file selection");
        return Task.CompletedTask;
    }
    public Task StartAsync(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Managers[id] = true;
        Calls.Enqueue(("Start", id));
        return Task.CompletedTask;
    }
    public Task PauseAsync(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Managers[id] = false;
        Calls.Enqueue(("Pause", id));
        return Task.CompletedTask;
    }
    public Task RemoveAsync(Guid id, bool deleteFiles, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Managers.TryRemove(id, out _);
        Calls.Enqueue((deleteFiles ? "DeleteFiles" : "Remove", id));
        return Task.CompletedTask;
    }
}

internal sealed class TestStatuses(ITorrentRepository repository, TestRuntime runtime) : ITorrentRuntimeStatusProvider
{
    public async Task<IReadOnlyList<TorrentRuntimeStatus>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(item => new TorrentRuntimeStatus(item.Id,
            runtime.Managers.GetValueOrDefault(item.Id) ? TorrentLifecycleState.Downloading : TorrentLifecycleState.Stopped,
            null, 123, 45, 12)).ToList();
    public async Task<TorrentRuntimeStatus?> GetAsync(Guid id, CancellationToken ct) => (await GetAllAsync(ct)).FirstOrDefault(s => s.TorrentId == id);
}

internal sealed class SessionFixture : IAsyncDisposable
{
    public TemporaryStorage Storage { get; } = new();
    public TestRuntime Runtime { get; } = new();
    public TestEngine Engine { get; } = new();
    public ShutdownMarker Marker { get; } = new();
    public IHost Host { get; }
    public ITorrentRepository Repository => Host.Services.GetRequiredService<ITorrentRepository>();
    public ITorrentSession Session => Host.Services.GetRequiredService<ITorrentSession>();
    public ITorrentRuntimeStateSource Statuses => Host.Services.GetRequiredService<ITorrentRuntimeStateSource>();
    private bool _stopped;

    public SessionFixture()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddNiTorrentInfrastructure();
        builder.Services.AddSingleton<IAppStorageService>(Storage);
        builder.Services.AddSingleton<ISettingsRepository, MemorySettings>();
        builder.Services.AddSingleton<ITorrentRuntimeGateway>(Runtime);
        builder.Services.AddSingleton<ITorrentEngineLifecycle>(Engine);
        builder.Services.AddSingleton<IShutdownStateService>(Marker);
        builder.Services.AddSingleton<ITorrentRuntimeStatusProvider>(sp => new TestStatuses(Repository, Runtime));
        Host = builder.Build();
    }
    public async Task<TorrentDownload> AddAsync(TorrentDownloadStatus status = TorrentDownloadStatus.Running)
    {
        var item = new TorrentDownload(Guid.NewGuid(), Guid.NewGuid().ToString("N"), "Example", Storage.Root) { Status = status };
        await Repository.AddAsync(item, new TorrentSource.TorrentBytes([1, 2, 3]), CancellationToken.None);
        return item;
    }
    public static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static async Task UntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!predicate()) await Task.Delay(10, timeout.Token);
    }
    public async Task StopAsync()
    {
        _stopped = true;
        await Host.StopAsync();
    }
    public async ValueTask DisposeAsync()
    {
        try { if (!_stopped) await Host.StopAsync(); }
        finally { Host.Dispose(); Storage.Dispose(); }
    }
}
