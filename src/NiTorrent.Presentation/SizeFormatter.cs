using System;

namespace NTorrent.App.Styles;

public static class SizeFormatter
{
    private static readonly string[] Units =
        { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

    public static string FormatBytes(long bytes, int decimals = 1)
    {
        if (bytes < 0)
            return "-" + FormatBytes(-bytes, decimals);

        double value = bytes;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < Units.Length - 1)
        {
            value /= 1024.0;
            unitIndex++;
        }

        return  $"{Math.Round(value, decimals)} {Units[unitIndex]}";
    }

    public static string FormatSpeed(long bytesPerSecond, int decimals = 2)
    {
        return FormatBytes(bytesPerSecond, decimals) + "/s";
    }

    public static string Format(long bytes, SizeUnit unit) 
    { 
        return unit switch 
        { 
            SizeUnit.B => $"{bytes} B",
            SizeUnit.KB => $"{bytes / 1024.0:F2} KB", 
            SizeUnit.MB => $"{bytes / 1024.0 / 1024.0:F2} MB",
            SizeUnit.GB => $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB", 
            _ => $"{bytes} B" }; }
    public static long Parse(double value, SizeUnit unit) 
    { 
        return unit switch 
        { 
            SizeUnit.B => (long)value,
            SizeUnit.KB => (long)(value * 1024), 
            SizeUnit.MB => (long)(value * 1024 * 1024), 
            SizeUnit.GB => (long)(value * 1024 * 1024 * 1024), 
            _ => (long)value 
        }; 
    }

    public static double ToUnit(long bytes, SizeUnit unit) 
    {
        return unit switch 
        {
            SizeUnit.B => bytes, 
            SizeUnit.KB => bytes / 1024.0,
            SizeUnit.MB => bytes / 1024.0 / 1024.0,
            SizeUnit.GB => bytes / 1024.0 / 1024.0 / 1024.0,
            _ => bytes 
        };
    }
}

public enum SizeUnit
{
    B,
    KB,
    MB,
    GB
}


