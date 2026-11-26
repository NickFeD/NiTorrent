using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NiTorrent.App.Services;

internal static partial class NativeTrayWindow
{
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int GetWindowLong(nint window, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int SetWindowLong(nint window, int index, int value);

    public static void HideFromTaskbar(nint window)
    {
        const int extendedStyle = -20;
        var style = GetWindowLong(window, extendedStyle);
        var error = Marshal.GetLastPInvokeError();
        // Zero is a valid style/previous value. Only zero with a last error indicates failure.
        if (style == 0 && error != 0) throw new Win32Exception(error);
        var previous = SetWindowLong(window, extendedStyle, (style | 0x80) & ~0x40000);
        error = Marshal.GetLastPInvokeError();
        if (previous == 0 && error != 0) throw new Win32Exception(error);
    }
}
