using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Torrents.Contracts;

namespace NiTorrent.Presentation.Features.Torrents;

public sealed class TorrentItemViewModelFactory : ITorrentItemViewModelFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TorrentItemViewModelFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public TorrentItemViewModel Create(TorrentListItem item, Func<TorrentItemViewModel, bool, Task> removeAsync)
    {
        return ActivatorUtilities.CreateInstance<TorrentItemViewModel>(
            _serviceProvider,
            item,
            removeAsync);
    }
}
