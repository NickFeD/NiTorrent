namespace NiTorrent.Presentation.Features.Torrents.Tree;

internal static class PreviewSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

    public static string Format(long bytes)
    {
        double value = bytes;
        int unit = 0;
        while (value >= 1000 && unit < Units.Length - 1)
        {
            value /= 1000;
            unit++;
        }
        if (Math.Round(value, 1) >= 1000 && unit < Units.Length - 1)
        {
            value /= 1000;
            unit++;
        }
        return $"{value:0.#} {Units[unit]}";
    }
}
