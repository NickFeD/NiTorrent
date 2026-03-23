using NiTorrent.Application.Torrents;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Infrastructure.Settings;

public sealed class TorrentEntrySettingsConfigRepository(TorrentEntrySettingsConfig config) : ITorrentEntrySettingsRepository
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

    public void Save(TorrentId torrentId, TorrentEntrySettings settings)
    {
        lock (_sync)
        {
            var items = new Dictionary<string, TorrentEntrySettings>(config.Items, StringComparer.OrdinalIgnoreCase);
            if (settings.IsDefault())
                items.Remove(torrentId.ToString());
            else
                items[torrentId.ToString()] = settings;

            config.Items = items;
            config.Save();
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
