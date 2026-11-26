namespace NiTorrent.Domain.Torrents;

public readonly record struct SavePath
{
    public string Value { get; }

    public SavePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Save path cannot be empty.", nameof(value));

        Value = value.Trim();
    }

    public override string ToString() => Value;

    public static implicit operator string(SavePath path) => path.Value;
    public static implicit operator SavePath(string value) => new(value);
}
