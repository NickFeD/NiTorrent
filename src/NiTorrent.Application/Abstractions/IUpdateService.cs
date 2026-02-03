namespace NiTorrent.Application.Abstractions;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken ct = default);
    Uri GetDefaultReleasesUri();
}
