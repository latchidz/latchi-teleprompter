using System.Windows;
using LatchiTeleprompter.App.Services;
using LatchiTeleprompter.App.Smoke;

namespace LatchiTeleprompter.App;

public partial class App : Application
{
    public static bool IsSmokeMode { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        if (SmokeRunner.ShouldRun(e.Args))
        {
            IsSmokeMode = true;
            var exitCode = SmokeRunner.Run();
            Shutdown(exitCode);
            return;
        }
        base.OnStartup(e);
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.DataDir);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppPaths.DataDir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}\n---\n");
        }
        catch { /* logging must never crash the crash handler */ }

        if (IsSmokeMode)
        {
            Shutdown(2);
            return;
        }
        MessageBox.Show(
            "حدث خطأ غير متوقع، لكن نصّك محفوظ تلقائياً ولن يضيع.\n\n" + e.Exception.Message,
            "LATCHI TELEPROMPTER", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
