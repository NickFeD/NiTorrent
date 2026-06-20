using System.Text.Json;
using System.Text.Json.Serialization;
using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services.AppLifecycle;

public sealed partial class ShutdownStateService(IAppStorageService storage) : IShutdownStateService
{
    private readonly IAppStorageService _storage = storage;

    public async Task<PreviousShutdownState> ReadPreviousStateAsync(CancellationToken cancellationToken)
    {
        var path = GetMarkerPath();

        if (!File.Exists(path))
            return PreviousShutdownState.Unknown;

        try
        {
            await using var stream = File.OpenRead(path);
            var marker = await JsonSerializer.DeserializeAsync(
                stream,
                ShutdownMarkerJsonContext.Default.ShutdownMarker,
                cancellationToken).ConfigureAwait(false);

            return marker?.State switch
            {
                ShutdownMarkerState.Clean => PreviousShutdownState.Clean,
                ShutdownMarkerState.Dirty => PreviousShutdownState.Unclean,
                _ => PreviousShutdownState.Unknown
            };
        }
        catch
        {
            return PreviousShutdownState.Unknown;
        }
    }

    public Task MarkStartedAsync(CancellationToken cancellationToken)
        => WriteMarkerAsync(ShutdownMarkerState.Dirty, cancellationToken);

    public Task MarkCleanShutdownAsync(CancellationToken cancellationToken)
        => WriteMarkerAsync(ShutdownMarkerState.Clean, cancellationToken);

    private async Task WriteMarkerAsync(ShutdownMarkerState state, CancellationToken cancellationToken)
    {
        var path = GetMarkerPath();
        _storage.EnsureParentDirectory(path);

        var marker = new ShutdownMarker
        {
            State = state,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(
            stream,
            marker,
            ShutdownMarkerJsonContext.Default.ShutdownMarker,
            cancellationToken).ConfigureAwait(false);
    }

    private string GetMarkerPath()
        => _storage.GetLocalPath(Path.Combine("Lifecycle", "shutdown-state.json"));

    private sealed class ShutdownMarker
    {
        public ShutdownMarkerState State { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ShutdownMarkerState>))]
    private enum ShutdownMarkerState
    {
        Dirty,
        Clean
    }

    [JsonSerializable(typeof(ShutdownMarker))]
    private sealed partial class ShutdownMarkerJsonContext : JsonSerializerContext;
}
