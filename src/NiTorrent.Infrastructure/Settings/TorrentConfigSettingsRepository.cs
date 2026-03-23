using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Infrastructure.Settings;

public sealed class TorrentConfigSettingsRepository(TorrentConfig config) : ITorrentSettingsRepository
{
    public Task<TorrentSettingsDraft> LoadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(ToDraft(config));
    }

    public Task SaveAsync(TorrentSettingsDraft settings, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        config.DefaultDownloadPath = settings.DefaultDownloadPath;
        config.MaximumDownloadRate = settings.MaximumDownloadRate;
        config.MaximumUploadRate = settings.MaximumUploadRate;
        config.MaximumDiskReadRate = settings.MaximumDiskReadRate;
        config.MaximumDiskWriteRate = settings.MaximumDiskWriteRate;
        config.AllowDht = settings.AllowDht;
        config.AllowPortForwarding = settings.AllowPortForwarding;
        config.AllowLocalPeerDiscovery = settings.AllowLocalPeerDiscovery;
        config.MaximumConnections = settings.MaximumConnections;
        config.MaximumOpenFiles = settings.MaximumOpenFiles;
        config.AutoSaveLoadFastResume = settings.AutoSaveLoadFastResume;
        config.AutoSaveLoadMagnetLinkMetadata = settings.AutoSaveLoadMagnetLinkMetadata;
        config.FastResumeMode = settings.FastResumeMode;
        config.MinimizeToTrayOnClose = settings.CloseBehavior == AppCloseBehavior.MinimizeToTray;
        config.Save();
        return Task.CompletedTask;
    }

    private static TorrentSettingsDraft ToDraft(TorrentConfig config)
        => new(
            config.DefaultDownloadPath,
            config.MaximumDownloadRate,
            config.MaximumUploadRate,
            config.MaximumDiskReadRate,
            config.MaximumDiskWriteRate,
            config.AllowDht,
            config.AllowPortForwarding,
            config.AllowLocalPeerDiscovery,
            config.MaximumConnections,
            config.MaximumOpenFiles,
            config.AutoSaveLoadFastResume,
            config.AutoSaveLoadMagnetLinkMetadata,
            config.FastResumeMode,
            config.MinimizeToTrayOnClose ? AppCloseBehavior.MinimizeToTray : AppCloseBehavior.ExitApplication);
}
