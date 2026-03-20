using Microsoft.Extensions.Logging;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentEngineStateStore
{
    private readonly ILogger<TorrentEngineStateStore> _logger;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly string _stateFilePath;

    public TorrentEngineStateStore(ILogger<TorrentEngineStateStore> logger, IAppStorageService storage)
    {
        _logger = logger;
        _stateFilePath = storage.GetLocalPath(@"Torrents\torrent_engine.dat");
        storage.EnsureParentDirectory(_stateFilePath);
    }

    public async Task SaveAsync(ClientEngine engine, CancellationToken ct = default)
    {
        await _saveGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await engine.SaveStateAsync(_stateFilePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save torrent engine state");
            if (!ct.IsCancellationRequested)
                throw;
        }
        finally
        {
            _saveGate.Release();
        }
    }
}
