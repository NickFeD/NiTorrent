using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class StartTorrentUseCase(ITorrentCommandService commandService)
{
    public async Task ExecuteAsync(TorrentId id, CancellationToken ct = default)
    {
        var result = await commandService.StartAsync(id, ct).ConfigureAwait(false);
        EnsureSucceeded(result, id, "Не удалось запустить торрент.");
    }

    private static void EnsureSucceeded(TorrentCommandResult result, TorrentId id, string fallback)
    {
        if (result.Outcome is TorrentCommandOutcome.Success or TorrentCommandOutcome.Deferred)
            return;

        throw new InvalidOperationException(result.Message ?? fallback + $" ({id.Value})");
    }
}
