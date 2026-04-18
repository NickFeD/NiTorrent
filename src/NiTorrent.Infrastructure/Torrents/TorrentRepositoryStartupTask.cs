using System;
using System.Collections.Generic;
using System.Text;
using NiTorrent.Application;
using NiTorrent.Application.Torrents.Abstract;

namespace NiTorrent.Infrastructure.Torrents;

internal class TorrentRepositoryStartupTask(ITorrentRepository torrentRepository) : IAppStartupTask
{
    public StartupStage Stage => StartupStage.Critical;

    public int Order => 999;

    public bool CanRunInParallel => false;

    public Task ExecuteAsync(CancellationToken ct)
    {
        return torrentRepository.LoadingAsync(ct);
    }
}
