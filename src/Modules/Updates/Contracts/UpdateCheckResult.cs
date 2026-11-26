namespace NiTorrent.Application.Abstractions;

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string StatusMessage,
    string? ChangeLog,
    Uri? DownloadUri);
