using NiTorrent.Torrents.Contracts;

namespace NiTorrent.Application.Settings;

public sealed class TorrentSettingsModule(AppSettingsService settings, GetSettingsQuery query,
    UpdateSettingsUseCase update) : ITorrentSettings
{
    public AppSettings Current => Copy(settings.Current);
    public async Task<AppSettings> GetAsync(CancellationToken ct = default) =>
        Copy(await query.ExecuteAsync(ct).ConfigureAwait(false));
    public Task UpdateAsync(AppSettings value, CancellationToken ct = default) =>
        update.ExecuteAsync(new(Copy(value)), ct);
    public Task SaveAsync(AppSettings value, CancellationToken ct = default) => settings.UpdateAsync(Copy(value), ct);

    private static AppSettings Copy(AppSettings value) => new()
    {
        CloseBehavior = value.CloseBehavior,
        EngineSettings = new()
        {
            MaximumDownloadRate = value.EngineSettings.MaximumDownloadRate,
            MaximumUploadRate = value.EngineSettings.MaximumUploadRate,
            MaximumDiskReadRate = value.EngineSettings.MaximumDiskReadRate,
            MaximumDiskWriteRate = value.EngineSettings.MaximumDiskWriteRate,
            AllowDht = value.EngineSettings.AllowDht,
            AllowPortForwarding = value.EngineSettings.AllowPortForwarding,
            AllowLocalPeerDiscovery = value.EngineSettings.AllowLocalPeerDiscovery,
            MaximumConnections = value.EngineSettings.MaximumConnections,
            MaximumOpenFiles = value.EngineSettings.MaximumOpenFiles,
            AutoSaveLoadFastResume = value.EngineSettings.AutoSaveLoadFastResume,
            AutoSaveLoadMagnetLinkMetadata = value.EngineSettings.AutoSaveLoadMagnetLinkMetadata,
            FastResumeMode = value.EngineSettings.FastResumeMode
        }
    };
}
