using System;
using System.Threading;
using System.Threading.Tasks;

public static class MainThreadDispatcher
{
    private static SynchronizationContext _mainThreadContext = default;

    private static readonly Exception _invalidException = new InvalidOperationException();

    public static void SetMainThreadContext()
    {
        var current = SynchronizationContext.Current;
        _mainThreadContext = current ?? throw _invalidException;
    }

    public static void Post(Action action)
    {
        if (_mainThreadContext == null) { throw _invalidException; }

        _mainThreadContext.Post(_ => action(), null);
    }

    public static Task<TResult> RunAsync<TResult>(Func<Task<TResult>> func)
    {
        var tcs = new TaskCompletionSource<TResult>();
        Post(async () =>
        {
            try
            {
                var res = await func();
                tcs.SetResult(res);
            }
            catch (Exception exception) { tcs.SetException(exception); }
        });
        return tcs.Task;
    }
}
