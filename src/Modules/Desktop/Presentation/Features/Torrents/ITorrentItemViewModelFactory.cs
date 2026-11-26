using NiTorrent.Torrents.Contracts;

namespace NiTorrent.Presentation.Features.Torrents;

public interface ITorrentItemViewModelFactory
{
    TorrentItemViewModel Create(TorrentListItem item, Func<TorrentItemViewModel, bool, Task> removeAsync);
}
