namespace NiTorrent.Domain.Settings;

public sealed record GlobalTorrentSettings(
    string DefaultDownloadPath,
    int MaximumDownloadRate,
    int MaximumUploadRate,
    int MaximumDiskReadRate,
    int MaximumDiskWriteRate,
    bool AllowDht,
    bool AllowPortForwarding,
    bool AllowLocalPeerDiscovery,
    int MaximumConnections,
    int MaximumOpenFiles,
    bool AutoSaveLoadFastResume,
    bool AutoSaveLoadMagnetLinkMetadata,
    TorrentFastResumeMode FastResumeMode,
    AppCloseBehavior CloseBehavior)
{
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DefaultDownloadPath))
        {
            errors.Add("Default download path is required.");
        }

        if (MaximumDownloadRate < 0) errors.Add("Maximum download rate cannot be negative.");
        if (MaximumUploadRate < 0) errors.Add("Maximum upload rate cannot be negative.");
        if (MaximumDiskReadRate < 0) errors.Add("Maximum disk read rate cannot be negative.");
        if (MaximumDiskWriteRate < 0) errors.Add("Maximum disk write rate cannot be negative.");
        if (MaximumConnections <= 0) errors.Add("Maximum connections must be greater than zero.");
        if (MaximumOpenFiles <= 0) errors.Add("Maximum open files must be greater than zero.");

        return errors;
    }
}
