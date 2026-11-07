using System.Windows;
using System.Windows.Threading;
using Mue.Wpf;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Mue.Tests;

internal static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> Host = new(Start);
    private static Dispatcher Start()
    {
        var ready = new TaskCompletionSource<Dispatcher>();
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            MueStyles.AddMissingDefaults(app);
            ready.SetResult(Dispatcher.CurrentDispatcher);
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    }

    public static Task Run(Action body) => Host.Value.InvokeAsync(body).Task.WaitAsync(TimeSpan.FromSeconds(30));

    public static void Pump(Task task)
    {
        var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        if (!task.IsCompleted) Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    public static bool? Show(UpdateBlockersDialog dialog, Action<UpdateBlockersDialog> action)
    {
        Exception? failure = null;
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(() =>
        {
            try { action(dialog); }
            catch (Exception ex) { failure = ex; }
            finally { if (dialog.IsVisible) dialog.Close(); }
        });
        var result = dialog.ShowDialog();
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }
}
