using NiTorrent.Application.Abstractions;
using NiTorrent.Application.Torrents.Commands;
using NiTorrent.Domain.Torrents;

namespace NiTorrent.Application.Torrents;

public sealed class PauseTorrentUseCase(ITorrentCommandService commandService)
{
    public async Task ExecuteAsync(TorrentId id, CancellationToken ct = default)
    {
        var result = await commandService.PauseAsync(id, ct).ConfigureAwait(false);
        EnsureSucceeded(result, id, "Не удалось поставить торрент на паузу.");
    }

    private static void EnsureSucceeded(TorrentCommandResult result, TorrentId id, string fallback)
    {
        if (result.Outcome is TorrentCommandOutcome.Success or TorrentCommandOutcome.Deferred)
            return;

        throw new InvalidOperationException(result.Message ?? fallback + $" ({id.Value})");
    }
}
