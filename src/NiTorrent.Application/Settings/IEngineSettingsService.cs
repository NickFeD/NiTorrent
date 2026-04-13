namespace NiTorrent.Application.Settings;

public interface IEngineSettingsService
{
    Task InitializeAsync(CancellationToken ct);
    Task ApplySettingsAsync(TorrentEngineSettings settings, CancellationToken ct);

}
