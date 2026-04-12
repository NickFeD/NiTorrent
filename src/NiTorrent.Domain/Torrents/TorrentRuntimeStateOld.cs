namespace NiTorrent.Domain.Torrents;

public class TorrentRuntimeStateOld
{
    private object error;
    private int v1;
    private int v2;
    private string missingSourceError;
    private bool v3;

    public TorrentRuntimeStateOld(object error, bool isComplete, double progress, int v1, int v2, string missingSourceError, bool v3)
    {
        this.error = error;
        IsComplete = isComplete;
        Progress = progress;
        this.v1 = v1;
        this.v2 = v2;
        this.missingSourceError = missingSourceError;
        this.v3 = v3;
    }

    public TorrentLifecycleStateOld LifecycleState { get; internal set; }
    public bool IsComplete { get; internal set; }
    public double Progress { get; internal set; }
    public long DownloadRateBytesPerSecond { get; internal set; }
    public long UploadRateBytesPerSecond { get; internal set; }
    public string? Error { get; internal set; }
    public bool IsEngineBacked { get; internal set; }
}
