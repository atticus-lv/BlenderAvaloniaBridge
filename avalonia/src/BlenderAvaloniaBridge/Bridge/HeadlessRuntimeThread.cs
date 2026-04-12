using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Headless;

namespace BlenderAvaloniaBridge.Bridge;

public sealed class HeadlessRuntimeThread : IDisposable
{
    private static readonly Lazy<HeadlessRuntimeThread> SharedInstance = new(() => new HeadlessRuntimeThread(isShared: true));

    private readonly BlockingCollection<Action> _workQueue = new();
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _thread;
    private readonly bool _isShared;
    private Exception? _startupException;

    public static HeadlessRuntimeThread Shared => SharedInstance.Value;

    public HeadlessRuntimeThread()
        : this(isShared: false)
    {
    }

    private HeadlessRuntimeThread(bool isShared)
    {
        _isShared = isShared;
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "BlenderAvaloniaHeadlessRuntime"
        };
        if (OperatingSystem.IsWindows())
        {
            _thread.SetApartmentState(ApartmentState.STA);
        }
        _thread.Start();
        _ready.Wait();
        if (_startupException is not null)
        {
            throw new InvalidOperationException("Failed to initialize Avalonia headless runtime.", _startupException);
        }
    }

    public Task InvokeAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _workQueue.Add(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _workQueue.Add(() =>
        {
            try
            {
                tcs.SetResult(action());
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public void Dispose()
    {
        if (_isShared)
        {
            return;
        }

        _workQueue.CompleteAdding();
        if (!_thread.Join(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("Timed out waiting for Avalonia runtime thread to stop.");
        }
        _ready.Dispose();
    }

    private void ThreadMain()
    {
        try
        {
            AppBuilder.Configure<App>()
                .UseSkia()
                .UseHarfBuzz()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                })
                .LogToTrace()
                .SetupWithoutStarting();
        }
        catch (Exception ex)
        {
            _startupException = ex;
            _ready.Set();
            return;
        }

        _ready.Set();

        foreach (var action in _workQueue.GetConsumingEnumerable())
        {
            action();
        }
    }
}
