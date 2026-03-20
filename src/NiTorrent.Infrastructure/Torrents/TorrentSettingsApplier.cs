using MonoTorrent.Client;
using NiTorrent.Application.Abstractions;
using NiTorrent.Domain.Settings;

namespace NiTorrent.Infrastructure.Torrents;

public sealed class TorrentSettingsApplier
{
    private readonly ITorrentPreferences _prefs;

    public TorrentSettingsApplier(ITorrentPreferences prefs)
    {
        _prefs = prefs;
    }

    public Task ApplyAsync(ClientEngine engine, string cacheDir)
    {
        var builder = new EngineSettingsBuilder(engine.Settings)
        {
            CacheDirectory = cacheDir,

            MaximumDownloadRate = _prefs.MaximumDownloadRate,
            MaximumUploadRate = _prefs.MaximumUploadRate,

            MaximumDiskReadRate = _prefs.MaximumDiskReadRate,
            MaximumDiskWriteRate = _prefs.MaximumDiskWriteRate,

            AllowPortForwarding = _prefs.AllowPortForwarding,
            AllowLocalPeerDiscovery = _prefs.AllowLocalPeerDiscovery,

            MaximumConnections = _prefs.MaximumConnections,
            MaximumOpenFiles = _prefs.MaximumOpenFiles,

            AutoSaveLoadFastResume = _prefs.AutoSaveLoadFastResume,
            AutoSaveLoadMagnetLinkMetadata = _prefs.AutoSaveLoadMagnetLinkMetadata,

            FastResumeMode = _prefs.FastResumeMode == TorrentFastResumeMode.Accurate
                ? MonoTorrent.Client.FastResumeMode.Accurate
                : MonoTorrent.Client.FastResumeMode.BestEffort
        };

        return engine.UpdateSettingsAsync(builder.ToSettings());
    }
}
