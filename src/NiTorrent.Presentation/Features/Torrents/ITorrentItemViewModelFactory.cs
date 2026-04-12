using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Features.Torrents;

public interface ITorrentItemViewModelFactory
{
    TorrentItemViewModel Create(TorrentDownload item, Func<TorrentItemViewModel, bool, Task> removeAsync);
}
