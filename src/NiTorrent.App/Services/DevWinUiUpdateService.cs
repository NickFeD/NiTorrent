using NiTorrent.Application.Abstractions;

namespace NiTorrent.App.Services;

public sealed class DevWinUiUpdateService : IUpdateService
{
    // TODO: заполни на свой репозиторий
    private const string Owner = "Ghost1372";
    private const string Repo = "DevWinUI";

    public Uri GetDefaultReleasesUri()
        => new($"https://github.com/{Owner}/{Repo}/releases");

    public async Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken ct = default)
    {
        if (!NetworkHelper.IsNetworkAvailable())
            return new UpdateCheckResult(false, "Error Connection", null, null);

        // DevWinUI helper
        var update = await UpdateHelper.CheckUpdateAsync(Owner, Repo, currentVersion);

        if (update.StableRelease.IsExistNewVersion)
        {
            return new UpdateCheckResult(
                true,
                $"New version {update.StableRelease.TagName} (Created {update.StableRelease.CreatedAt}, Published {update.StableRelease.PublishedAt})",
                update.StableRelease.Changelog,
                GetDefaultReleasesUri());
        }

        if (update.PreRelease.IsExistNewVersion)
        {
            return new UpdateCheckResult(
                true,
                $"New pre-release {update.PreRelease.TagName} (Created {update.PreRelease.CreatedAt}, Published {update.PreRelease.PublishedAt})",
                update.PreRelease.Changelog,
                GetDefaultReleasesUri());
        }

        return new UpdateCheckResult(false, "You are using latest version", null, null);
    }
}
