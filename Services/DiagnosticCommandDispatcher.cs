using System.Collections.Concurrent;
using System.Text;

namespace MyBlazorApp1.Services;

/// <summary>
/// Serializes commands per physical TCP connection and correlates the next ◉-terminated
/// diagnostic frame with the command currently in flight on that connection.
/// </summary>
public sealed class DiagnosticCommandDispatcher : IDisposable
{
    private sealed class ConnectionState
    {
        public readonly SemaphoreSlim CommandLock = new(1, 1);
        public readonly object Sync = new();
        public readonly List<byte> Buffer = new();
        public TaskCompletionSource<byte[]>? PendingResponse;
    }

    private static readonly byte[] EndMarker = { 0xE2, 0x97, 0x89 }; // ◉
    private readonly DiagnosticTcpServerService _tcp;
    private readonly ConcurrentDictionary<string, ConnectionState> _states = new();

    public DiagnosticCommandDispatcher(DiagnosticTcpServerService tcp)
    {
        _tcp = tcp;
        _tcp.ClientDataReceived += OnDataReceived;
        _tcp.ClientDisconnected += OnDisconnected;
    }

    public async Task<string> ExecuteAsync(
        string connectionId,
        string command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException("Connection ID is required.", nameof(connectionId));
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command is required.", nameof(command));

        var state = _states.GetOrAdd(connectionId, _ => new ConnectionState());
        await state.CommandLock.WaitAsync(cancellationToken);
        try
        {
            var tcs = new TaskCompletionSource<byte[]>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            lock (state.Sync)
            {
                if (state.PendingResponse != null)
                    throw new InvalidOperationException("A command is already awaiting a response.");
                state.PendingResponse = tcs;
                // Remove stale, already-buffered data before a new request.
                state.Buffer.Clear();
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _tcp.SendAsync(connectionId, Encoding.UTF8.GetBytes(command + "\n"));
                var response = await tcs.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(15), cancellationToken);
                return Encoding.UTF8.GetString(response);
            }
            finally
            {
                lock (state.Sync)
                {
                    if (ReferenceEquals(state.PendingResponse, tcs))
                        state.PendingResponse = null;
                    state.Buffer.Clear();
                }
            }
        }
        finally
        {
            state.CommandLock.Release();
        }
    }

    private void OnDataReceived(string connectionId, byte[] data)
    {
        if (!_states.TryGetValue(connectionId, out var state))
            return;

        lock (state.Sync)
        {
            if (state.PendingResponse == null)
                return;

            state.Buffer.AddRange(data);
            while (true)
            {
                var end = FindEndMarker(state.Buffer);
                if (end < 0)
                    break;

                var frame = state.Buffer.Take(end).ToArray();
                state.Buffer.RemoveRange(0, end + EndMarker.Length);

                if (frame.Length > 0)
                {
                    state.PendingResponse.TrySetResult(frame);
                    return;
                }
            }

            if (state.Buffer.Count > 1024 * 1024)
            {
                state.Buffer.Clear();
                state.PendingResponse.TrySetException(
                    new InvalidDataException("Diagnostic response exceeded 1 MB."));
            }
        }
    }

    private void OnDisconnected(DiagnosticTcpServerService.ConnectedClientInfo client)
    {
        if (!_states.TryGetValue(client.ConnectionId, out var state))
            return;

        lock (state.Sync)
            state.PendingResponse?.TrySetException(
                new IOException("Diagnostic bridge disconnected before the response arrived."));
    }

    private static int FindEndMarker(List<byte> buffer)
    {
        for (var i = 0; i <= buffer.Count - EndMarker.Length; i++)
            if (buffer[i] == EndMarker[0] &&
                buffer[i + 1] == EndMarker[1] &&
                buffer[i + 2] == EndMarker[2])
                return i;
        return -1;
    }

    public void Dispose()
    {
        _tcp.ClientDataReceived -= OnDataReceived;
        _tcp.ClientDisconnected -= OnDisconnected;
        foreach (var state in _states.Values)
            state.CommandLock.Dispose();
        _states.Clear();
    }
}
