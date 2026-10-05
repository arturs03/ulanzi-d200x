namespace D200xDirect;

public static class DeviceSession
{
    public static readonly TimeSpan KeepAwakeInterval = TimeSpan.FromSeconds(5);

    // A reader failure ends keep-awake writes; a writer failure cancels the pending read.
    // There are no reconnects, write retries or overlapping timer callbacks.
    public static async Task RunPairAsync(Func<CancellationToken, Task> listen,
        Func<CancellationToken, Task> keepAwake, CancellationToken cancellation)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        async Task Invoke(Func<CancellationToken, Task> operation) => await operation(stop.Token);
        var reader = Invoke(listen);
        var writer = Invoke(keepAwake);
        var first = await Task.WhenAny(reader, writer);
        try { await first; }
        finally
        {
            stop.Cancel();
            try { await Task.WhenAll(reader, writer); } catch { /* Preserve the first operation's outcome. */ }
        }
    }

    public static async Task KeepAwakeAsync(Func<CancellationToken, Task> write,
        TimeSpan interval, CancellationToken cancellation)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(cancellation))
        {
            cancellation.ThrowIfCancellationRequested();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            try { await write(deadline.Token); }
            catch (OperationCanceledException error) when (!cancellation.IsCancellationRequested)
            { throw new TimeoutException("Keep-awake write exceeded its 3-second deadline. Device control stopped.", error); }
        }
    }
}
