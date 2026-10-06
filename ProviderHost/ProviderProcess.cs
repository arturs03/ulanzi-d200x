using System.Diagnostics;
using System.Threading.Channels;

namespace D200xDirect.Providers;

public sealed class ProviderProcess : IAsyncDisposable
{
    readonly InstalledProvider installed;
    readonly Process process;
    readonly ProviderJob job;
    readonly CancellationTokenSource lifetime = new();
    readonly SemaphoreSlim requestGate = new(1, 1);
    readonly Channel<ProviderMessage> responses = Channel.CreateBounded<ProviderMessage>(1);
    readonly Task outputTask, errorTask;
    int sequence, pendingId, state = (int)ProviderState.Starting;
    long diagnosticBytes;
    bool disposed;
    long lastSnapshot;
    readonly TimeSpan requestDeadline;
    public ProviderState State => (ProviderState)Volatile.Read(ref state);
    public ProviderValues Values { get; } = new();
    // Diagnostics are drained and counted but deliberately not retained as raw third-party text.
    public long DiagnosticBytes => Interlocked.Read(ref diagnosticBytes);
    public int ProcessId => process.Id;

    ProviderProcess(InstalledProvider installed, Process process, ProviderJob job, TimeSpan requestDeadline)
    {
        this.installed = installed; this.process = process; this.job = job; this.requestDeadline = requestDeadline;
        outputTask = ReadOutputAsync(); errorTask = DrainErrorsAsync();
    }

    public static async Task<ProviderProcess> StartAsync(InstalledProvider provider, CancellationToken token, TimeSpan? deadline = null, SensorLogSettings? sensorLog = null)
    {
        token.ThrowIfCancellationRequested();
        if (sensorLog is not null)
        {
            if (provider.Manifest.Id != "d200x.system" || !provider.Manifest.Capabilities.Contains("read-ohm-log")) throw new ArgumentException("This provider does not support sensor logs.");
            sensorLog.Validate();
        }
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) throw new PlatformNotSupportedException("Providers require Windows x64.");
        var checkedProvider = ProviderDiscovery.Load(provider.Directory);
        if (checkedProvider.Manifest != provider.Manifest
            && System.Text.Json.JsonSerializer.Serialize(checkedProvider.Manifest, ProviderContract.Json)
                != System.Text.Json.JsonSerializer.Serialize(provider.Manifest, ProviderContract.Json))
            throw new IOException("Provider metadata changed; discover it again before enabling.");
        var info = new ProcessStartInfo(Path.Combine(provider.Directory, provider.Manifest.Executable))
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = provider.Directory,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.Environment.Clear();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP" })
            if (Environment.GetEnvironmentVariable(name) is { } value) info.Environment[name] = value;
        var process = new Process { StartInfo = info };
        ProviderProcess? supervisor = null;
        try
        {
            if (!process.Start()) throw new IOException("Provider could not start.");
            var job = ProviderJob.Own(process);
            supervisor = new(provider, process, job, deadline ?? ProviderContract.RequestDeadline);
            var hello = await supervisor.ExchangeAsync("hello", null, token, sensorLog).ConfigureAwait(false);
            if (hello.ProviderId != provider.Manifest.Id || hello.Samples is not null || hello.Selections is not null || hello.SensorLog is not null)
                throw new IOException("Provider hello identity or shape is invalid.");
            if (Interlocked.CompareExchange(ref supervisor.state, (int)ProviderState.Running, (int)ProviderState.Starting) != (int)ProviderState.Starting)
                throw new IOException("Provider exited during hello.");
            return supervisor;
        }
        catch
        {
            if (supervisor is not null) await supervisor.DisposeAsync().ConfigureAwait(false);
            else { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } process.Dispose(); }
            throw;
        }
    }

    public async Task<IReadOnlyList<ProviderSample>> SnapshotAsync(IReadOnlyList<MetricSelection> selected, CancellationToken token)
    {
        // Own validation, cadence and exchange together; waiting must not admit overlapping requests.
        if (!await requestGate.WaitAsync(0, token).ConfigureAwait(false)) throw new InvalidOperationException("A provider request is already pending.");
        try { return await SnapshotOwnedAsync(selected, token).ConfigureAwait(false); }
        finally { requestGate.Release(); }
    }

    async Task<IReadOnlyList<ProviderSample>> SnapshotOwnedAsync(IReadOnlyList<MetricSelection> selected, CancellationToken token)
    {
        if (State != ProviderState.Running) throw new IOException("Provider is not active; enable it again.");
        var selection = selected.ToArray();
        ValidateSelections(selection);
        var minimum = selection.Max(x => installed.Manifest.Metrics.Single(m => m.Id == x.MetricId).MinimumIntervalMs);
        // Windows timers can wake early. Recheck a monotonic clock before sending, rather than
        // treating normal timer rounding as a provider failure or shortening its minimum interval.
        using var cadence = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        while (lastSnapshot != 0)
        {
            var remaining = minimum - Stopwatch.GetElapsedTime(lastSnapshot).TotalMilliseconds;
            if (remaining <= 0) break;
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, Math.Ceiling(remaining))), cadence.Token).ConfigureAwait(false);
        }
        try
        {
            var message = await ExchangeOwnedAsync("snapshot", selection, token).ConfigureAwait(false);
            if (message.ProviderId is not null || message.Selections is not null || message.SensorLog is not null || message.Samples is null
                || message.Samples.Length != selection.Length) throw new IOException("Invalid provider snapshot shape.");
            var expected = selection.ToHashSet();
            foreach (var sample in message.Samples)
            {
                if (sample is null || !expected.Remove(new(sample.MetricId, sample.SourceId))) throw new IOException("Duplicate or unrequested provider sample.");
                var metric = installed.Manifest.Metrics.Single(x => x.Id == sample.MetricId);
                if (sample.Unit != metric.Unit || sample.Status is not ("ok" or "unavailable" or "error")
                    || sample.Code is { } code && !ProviderContract.IsId(code)) throw new IOException("Invalid sample unit or status.");
                if (sample.Status == "ok")
                {
                    if (sample.Value is not { } value || !double.IsFinite(value) || sample.ObservedAt is not { } observed
                        || observed.Offset != TimeSpan.Zero || observed > DateTimeOffset.UtcNow.AddSeconds(2)
                        || metric.Unit == "percent" && value is < 0 or > 100
                        || metric.Unit == "celsius" && value is < -50 or > 200
                        || metric.Unit is "usd" or "fps" && value < 0)
                        throw new IOException("Invalid sample value or observation time.");
                }
                else if (sample.Value is not null || sample.ObservedAt is not null) throw new IOException("Unavailable sample carries a reading.");
            }
            Values.Replace(message.Samples);
            if (State != ProviderState.Running) { Values.Clear(); throw new IOException("Provider exited during snapshot."); }
            lastSnapshot = Stopwatch.GetTimestamp();
            return Array.AsReadOnly(message.Samples);
        }
        catch { Fail(); await EndAsync().ConfigureAwait(false); throw; }
    }

    void ValidateSelections(MetricSelection[] selected)
    {
        if (selected.Length is 0 or > ProviderContract.MaximumMetrics || selected.Distinct().Count() != selected.Length)
            throw new ArgumentException("Choose 1–64 unique provider values.");
        foreach (var item in selected)
        {
            var metric = installed.Manifest.Metrics.SingleOrDefault(x => x.Id == item.MetricId);
            if (metric is null || !metric.SourceIds.Contains(item.SourceId, StringComparer.Ordinal))
                throw new ArgumentException("Unknown provider metric or source.");
        }
    }

    async Task<ProviderMessage> ExchangeAsync(string type, MetricSelection[]? selections, CancellationToken token, SensorLogSettings? sensorLog = null)
    {
        if (!await requestGate.WaitAsync(0, token).ConfigureAwait(false)) throw new InvalidOperationException("A provider request is already pending.");
        try { return await ExchangeOwnedAsync(type, selections, token, sensorLog).ConfigureAwait(false); }
        finally { requestGate.Release(); }
    }

    async Task<ProviderMessage> ExchangeOwnedAsync(string type, MetricSelection[]? selections, CancellationToken token, SensorLogSettings? sensorLog = null)
    {
        try
        {
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            bounded.CancelAfter(type == "shutdown" ? ProviderContract.ShutdownDeadline : requestDeadline);
            var id = checked(++sequence);
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new ProviderMessage
            { ProtocolVersion = ProviderContract.Version, RequestId = id, Type = type, Selections = selections, SensorLog = sensorLog }, ProviderContract.Json);
            if (bytes.Length > ProviderContract.MaximumBytes) throw new IOException("Provider request exceeds limits.");
            Volatile.Write(ref pendingId, id);
            await process.StandardInput.BaseStream.WriteAsync(bytes, bounded.Token).ConfigureAwait(false);
            await process.StandardInput.BaseStream.WriteAsync(new byte[] { 10 }, bounded.Token).ConfigureAwait(false);
            await process.StandardInput.BaseStream.FlushAsync(bounded.Token).ConfigureAwait(false);
            var response = await responses.Reader.ReadAsync(bounded.Token).ConfigureAwait(false);
            if (response.Type != type || response.RequestId != id) throw new IOException("Unexpected provider response.");
            return response;
        }
        catch (OperationCanceledException error) when (!token.IsCancellationRequested && !lifetime.IsCancellationRequested)
        { throw new TimeoutException("Provider response deadline elapsed.", error); }
    }

    async Task ReadOutputAsync()
    {
        Exception? failure = null;
        try
        {
            var reader = new BoundedFrames(process.StandardOutput.BaseStream);
            while (await reader.ReadAsync(lifetime.Token).ConfigureAwait(false) is { } frame)
            {
                var message = ProviderContract.Parse<ProviderMessage>(frame);
                if (message.ProtocolVersion != ProviderContract.Version || message.RequestId <= 0
                    || Interlocked.CompareExchange(ref pendingId, 0, message.RequestId) != message.RequestId
                    || !responses.Writer.TryWrite(message)) throw new IOException("Unsolicited, duplicate or incompatible provider response.");
            }
            if (State != ProviderState.Stopping && !lifetime.IsCancellationRequested) throw new IOException("Provider closed its output.");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error) { failure = error; Fail(); try { job.End(); } catch (System.ComponentModel.Win32Exception) { } }
        finally { responses.Writer.TryComplete(failure); }
    }

    async Task DrainErrorsAsync()
    {
        try
        {
            var buffer = new byte[4096];
            int count;
            while ((count = await process.StandardError.BaseStream.ReadAsync(buffer, lifetime.Token).ConfigureAwait(false)) > 0)
                Interlocked.Add(ref diagnosticBytes, count);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (IOException) { Fail(); try { job.End(); } catch (System.ComponentModel.Win32Exception) { } }
    }

    void Fail() { Volatile.Write(ref state, (int)ProviderState.Failed); Values.Clear(); }

    async Task EndAsync()
    {
        try { job.End(); } finally { job.Dispose(); lifetime.Cancel(); }
        await process.WaitForExitAsync().ConfigureAwait(false);
        await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            if (State == ProviderState.Running)
            {
                Volatile.Write(ref state, (int)ProviderState.Stopping);
                try
                {
                    using var deadline = new CancellationTokenSource(ProviderContract.ShutdownDeadline);
                    var reply = await ExchangeAsync("shutdown", null, deadline.Token).ConfigureAwait(false);
                    if (reply.ProviderId is not null || reply.Samples is not null || reply.Selections is not null || reply.SensorLog is not null) throw new IOException("Invalid shutdown response.");
                    process.StandardInput.Close();
                    await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (error is IOException or OperationCanceledException or TimeoutException or InvalidOperationException or ChannelClosedException) { }
            }
            await EndAsync().ConfigureAwait(false);
        }
        finally
        {
            Values.Clear(); Volatile.Write(ref state, (int)ProviderState.Stopped);
            process.Dispose(); lifetime.Dispose(); requestGate.Dispose();
        }
    }
}
