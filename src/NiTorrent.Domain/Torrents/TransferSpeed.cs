namespace NiTorrent.Domain.Torrents;

public readonly record struct TransferSpeed
{
    public long BytesPerSecond { get; }

    public TransferSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(bytesPerSecond), "Transfer speed cannot be negative.");

        BytesPerSecond = bytesPerSecond;
    }

    public override string ToString() => BytesPerSecond.ToString();

    public static implicit operator long(TransferSpeed speed) => speed.BytesPerSecond;
    public static implicit operator TransferSpeed(long bytesPerSecond) => new(bytesPerSecond);
}
