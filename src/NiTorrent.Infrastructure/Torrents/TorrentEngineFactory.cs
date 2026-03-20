using Microsoft.Extensions.Logging;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentEngineFactory
{
    private readonly ILogger<TorrentEngineFactory> _logger;
    private readonly IDialogService _dialogs;
    private readonly string _cacheDir;
    private readonly string _stateFilePath;

    public TorrentEngineFactory(
        ILogger<TorrentEngineFactory> logger,
        IDialogService dialogs,
        IAppStorageService storage)
    {
        _logger = logger;
        _dialogs = dialogs;
        _cacheDir = storage.GetCachePath(@"Torrents\cache");
        _stateFilePath = storage.GetLocalPath(@"Torrents\torrent_engine.dat");

        storage.EnsureDirectory(_cacheDir);
        storage.EnsureParentDirectory(_stateFilePath);
    }

    public async Task<ClientEngine> CreateWithRecoveryAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_stateFilePath))
            return CreateFresh();

        try
        {
            return await ClientEngine.RestoreStateAsync(_stateFilePath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var brokenPath = MoveBrokenStateFile();
            _logger.LogWarning(ex,
                "Failed to restore torrent engine state from {StateFilePath}. Backup created at {BrokenPath}",
                _stateFilePath,
                brokenPath);

            await _dialogs.ShowTextAsync(
                "Повреждён torrent_engine.dat",
                $"Сохранённое состояние движка повреждено или несовместимо. Будет выполнен запуск с чистым состоянием.\n\nРезервная копия: {brokenPath}").ConfigureAwait(false);

            return CreateFresh();
        }
    }

    public ClientEngine CreateFresh()
    {
        var settings = new EngineSettingsBuilder
        {
            CacheDirectory = _cacheDir,
        }.ToSettings();

        return new ClientEngine(settings);
    }

    private string MoveBrokenStateFile()
    {
        try
        {
            var brokenPath = Path.Combine(
                Path.GetDirectoryName(_stateFilePath)!,
                $"torrent_engine.broken-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.dat");

            if (File.Exists(brokenPath))
                File.Delete(brokenPath);

            File.Move(_stateFilePath, brokenPath, overwrite: true);
            return brokenPath;
        }
        catch (Exception moveEx)
        {
            _logger.LogWarning(moveEx, "Failed to move broken torrent engine state file {StateFilePath}", _stateFilePath);
            return _stateFilePath;
        }
    }
}
