using Microsoft.Windows.AppLifecycle;

namespace NiTorrent.App.Services.AppLifecycle;

public static class ActivationLogFormatter
{
    public static string Describe(AppActivationArguments activationArgs)
        => activationArgs.Kind.ToString();
}
