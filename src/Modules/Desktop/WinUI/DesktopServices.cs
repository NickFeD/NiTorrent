namespace NiTorrent.App;

/// <summary>View construction bridge for XAML-created pages, owned by Desktop.</summary>
public static class DesktopServices
{
    private static IServiceProvider? _services;
    public static void Initialize(IServiceProvider services) => _services = services;
    public static T GetService<T>() where T : class =>
        (_services ?? throw new InvalidOperationException("Desktop is not initialized.")).GetRequiredService<T>();
}
