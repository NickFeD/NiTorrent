using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Presentation.Features.Torrents;

public sealed class TorrentItemViewModelFactory : ITorrentItemViewModelFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TorrentItemViewModelFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public TorrentItemViewModel Create(TorrentDownload item, Func<TorrentItemViewModel, bool, Task> removeAsync)
    {
        return ActivatorUtilities.CreateInstance<TorrentItemViewModel>(
            _serviceProvider,
            item,
            removeAsync);
    }
}
