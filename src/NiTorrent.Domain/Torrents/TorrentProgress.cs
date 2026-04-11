using System.Globalization;

namespace NiTorrent.Domain.Torrents;

public readonly record struct TorrentProgress
{
    public double Value { get; }

    public TorrentProgress(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Progress must be a finite number.");

        if (value is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(value), "Progress must be between 0 and 100.");

        Value = value;
    }

    public override string ToString() => Value.ToString("F2", CultureInfo.InvariantCulture);

    public static implicit operator double(TorrentProgress progress) => progress.Value;
    public static implicit operator TorrentProgress(double value) => new(value);
}
