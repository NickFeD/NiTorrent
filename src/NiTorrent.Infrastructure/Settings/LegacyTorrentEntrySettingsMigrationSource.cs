using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Settings;

/// <summary>
/// Migration-only adapter over the legacy per-torrent settings config.
/// Used to import old settings into the product-owned torrent collection.
/// </summary>
public sealed class LegacyTorrentEntrySettingsMigrationSource(TorrentEntrySettingsConfig config)
    : ILegacyTorrentEntrySettingsMigrationSource
{
    private readonly object _sync = new();

    public TorrentEntrySettings Load(TorrentId torrentId)
    {
        lock (_sync)
        {
            if (config.Items.TryGetValue(torrentId.ToString(), out var settings) && settings is not null)
                return settings;

            return TorrentEntrySettings.Default;
        }
    }

    public void Remove(TorrentId torrentId)
    {
        lock (_sync)
        {
            if (!config.Items.ContainsKey(torrentId.ToString()))
                return;

            var items = new Dictionary<string, TorrentEntrySettings>(config.Items, StringComparer.OrdinalIgnoreCase);
            items.Remove(torrentId.ToString());
            config.Items = items;
            config.Save();
        }
    }
}
