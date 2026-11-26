namespace NiTorrent.Application.Abstractions;

public interface IUriLauncher
{
    Task LaunchAsync(Uri uri, CancellationToken ct = default);
}
