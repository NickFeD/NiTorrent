using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using NiTorrent.App.Services.AppLifecycle;
using WinUIApplication = Microsoft.UI.Xaml.Application;

namespace NiTorrent.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        EarlyLifecycleLogger.Log($"Process start. Args={args.Length}");

        WinRT.ComWrappersSupport.InitializeComWrappers();

        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        EarlyLifecycleLogger.Log($"Activation received: {ActivationLogFormatter.Describe(activationArgs)}");

        var mainInstance = AppInstance.FindOrRegisterForKey("NiTorrent");
        EarlyLifecycleLogger.Log(mainInstance.IsCurrent
            ? "Single-instance result: primary instance"
            : "Single-instance result: secondary instance; redirecting activation");

        if (!mainInstance.IsCurrent)
        {
            mainInstance.RedirectActivationToAsync(activationArgs).GetAwaiter().GetResult();
            EarlyLifecycleLogger.Log("Activation redirected to primary instance; secondary process exiting");
            return;
        }

        WinUIApplication.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
