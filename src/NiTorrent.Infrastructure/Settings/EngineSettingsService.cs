using System.Net;
using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Settings;
using NiTorrent.Domain.Settings;
using NiTorrent.Infrastructure.Torrents;

namespace NiTorrent.Infrastructure.Settings;

public class EngineSettingsService : IEngineSettingsService
{
    private readonly AppSettingsService _settingsService;
    private TorrentEngineCoordinator _torrentEngineCoordinator;
    private IAppStorageService _storage;
    private string? _cacheDir;

    public EngineSettingsService(AppSettingsService settings, TorrentEngineCoordinator torrentEngineCoordinator, IAppStorageService appStorageService)
    {
        _settingsService = settings;
        _torrentEngineCoordinator = torrentEngineCoordinator;
        _storage = appStorageService;
        settings.Changed += OnUpdateSettings;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var appSettings = _settingsService.Current;
        EngineSettingsBuilder engineSettingsBuilder = MapToBuilder(appSettings.EngineSettings);

        await _torrentEngineCoordinator.InitializeAsync(engineSettingsBuilder.ToSettings(), ct);
    }

    public Task ApplySettingsAsync(TorrentEngineSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var monoSettings = MapToBuilder(settings).ToSettings();

        return _torrentEngineCoordinator.ApplySettingsAsync(monoSettings, ct);
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

    private void OnUpdateSettings(AppSettings appSettings)
    {
        // HACK: Fire and forget, we don't want to await this and block the UI thread, but we also don't want to ignore it.
        var _ = ApplySettingsAsync(appSettings.EngineSettings, CancellationToken.None);
    }
}
