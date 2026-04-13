using System.Net;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Domain.Settings;
using NiTorrent.Infrastructure.Torrents;

namespace NiTorrent.Infrastructure.Settings;

public class EngineSettingsService(ISettingsRepository settings, TorrentEngineCoordinator torrentEngineCoordinator, IAppStorageService appStorageService) : IEngineSettingsService
{
    ISettingsRepository _settingsRepository = settings;
    private TorrentEngineSettings _current;
    private TorrentEngineCoordinator _torrentEngineCoordinator = torrentEngineCoordinator;
    private IAppStorageService _storage = appStorageService;
    private string? _cacheDir;

    public async Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var appSettings = await _settingsRepository.GetAppSettings(ct);
        EngineSettingsBuilder engineSettingsBuilder = MapToBuilder(appSettings.EngineSettings);

        await _torrentEngineCoordinator.InitializeAsync(engineSettingsBuilder.ToSettings(), ct);
        _current = appSettings.EngineSettings;
    }

    public async Task ApplySettingsAsync(TorrentEngineSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var monoSettings = MapToBuilder(settings).ToSettings();

        await _torrentEngineCoordinator.ApplySettingsAsync(monoSettings, ct);
        _current = settings;

    }

    private EngineSettingsBuilder MapToBuilder(TorrentEngineSettings source)
    {
        if (_cacheDir is null)
        {
            _cacheDir = _storage.GetCachePath(@"Torrents\cache");
            _storage.EnsureDirectory(_cacheDir);
        }
        var builder = new EngineSettingsBuilder
        {
            AutoSaveLoadDhtCache = true,
            MaximumHalfOpenConnections = 6,
            MaximumDownloadRate = source.MaximumDownloadRate,
            MaximumUploadRate = source.MaximumUploadRate,

            MaximumDiskReadRate = source.MaximumDiskReadRate,
            MaximumDiskWriteRate = source.MaximumDiskWriteRate,

            AllowLocalPeerDiscovery = source.AllowLocalPeerDiscovery,
            AllowPortForwarding = source.AllowPortForwarding,

            MaximumConnections = source.MaximumConnections,
            MaximumOpenFiles = source.MaximumOpenFiles,

            AutoSaveLoadFastResume = source.AutoSaveLoadFastResume,
            AutoSaveLoadMagnetLinkMetadata = source.AutoSaveLoadMagnetLinkMetadata,

            FastResumeMode = source.FastResumeMode == TorrentFastResumeMode.BestEffort
                ? FastResumeMode.BestEffort
                : FastResumeMode.Accurate,

            CacheDirectory = _cacheDir,

        };

        builder.DhtEndPoint = source.AllowDht
            ? new IPEndPoint(IPAddress.Any, 0)
            : null;

        return builder;
    }
}
