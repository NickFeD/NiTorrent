using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NiTorrent.App.Services;

// H.NotifyIcon does not expose deletion of a registration whose Create failed halfway.
internal static partial class NativeTrayCleanup
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct NotifyIconData
    {
        public uint Size;
        public nint Window;
        public uint Id;
        public uint Flags;
        public uint Callback;
        public nint Icon;
        public fixed char Tip[128];
        public uint State;
        public uint StateMask;
        public fixed char Info[256];
        public uint Version;
        public fixed char InfoTitle[64];
        public uint InfoFlags;
        public Guid Guid;
        public nint BalloonIcon;
    }

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvStdcall)])]
    private static partial int ShellNotifyIcon(uint message, in NotifyIconData data);

    public static unsafe bool Delete(Guid id)
    {
        var data = new NotifyIconData { Size = (uint)sizeof(NotifyIconData), Flags = 0x20, Guid = id };
        return ShellNotifyIcon(2, in data) != 0;
    }
}
