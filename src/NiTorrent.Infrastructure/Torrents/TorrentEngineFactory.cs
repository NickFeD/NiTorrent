using Microsoft.Extensions.Logging;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentEngineFactory
{
    private readonly ILogger<TorrentEngineFactory> _logger;
    private readonly string _cacheDir;

    public TorrentEngineFactory(
        ILogger<TorrentEngineFactory> logger,
        IAppStorageService storage)
    {
        _logger = logger;
        _cacheDir = storage.GetCachePath(@"Torrents\cache");
        storage.EnsureDirectory(_cacheDir);
    }

    public Task<ClientEngine> CreateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var settings = new EngineSettingsBuilder
        {
            CacheDirectory = _cacheDir,
            MaximumHalfOpenConnections = 8,
        }.ToSettings();

        _logger.LogInformation("Initialized empty torrent engine instance without engine-wide state restore");
        return Task.FromResult(new ClientEngine(settings));
    }
}
