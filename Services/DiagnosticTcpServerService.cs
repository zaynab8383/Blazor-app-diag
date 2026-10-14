//using System.Net;
//using System.Net.Sockets;
//using System.Threading;
//namespace MyBlazorApp1.Services;

//public class DiagnosticTcpServerService : IDisposable
//{
//    private readonly SemaphoreSlim _startLock = new(1, 1);
//    private TcpListener? _server;
//    private TcpClient? _client;
//    private NetworkStream? _stream;

//    private readonly object _lock = new();

//    private readonly SemaphoreSlim _sendLock =
//        new(1, 1);

//    public bool IsRunning { get; private set; }

//    public bool IsClientConnected
//    {
//        get
//        {
//            lock (_lock)
//            {
//                return
//                    _client != null &&
//                    _client.Connected &&
//                    _stream != null;
//            }
//        }
//    }

//    public string? ClientEndPoint
//    {
//        get
//        {
//            lock (_lock)
//            {
//                return _client?
//                    .Client?
//                    .RemoteEndPoint?
//                    .ToString();
//            }
//        }
//    }

//    public event Action<byte[]>? DataReceived;

//    public event Action<string>? LogReceived;

//    // =========================================================
//    // START
//    // =========================================================


//    public async Task StartAsync(int port = 9000)
//    {
//        await _startLock.WaitAsync();

//        try
//        {
//            if (IsRunning)
//            {
//                Console.WriteLine("TCP Server is already running.");
//                return;
//            }

//            var server = new TcpListener(IPAddress.Any, port);

//            try
//            {
//                server.Start();
//                _server = server;
//                IsRunning = true;
//            }
//            catch
//            {
//                server.Stop();
//                throw;
//            }

//            Console.WriteLine();
//            Console.WriteLine("=================================");
//            Console.WriteLine("       DIAGNOSTIC TCP SERVER");
//            Console.WriteLine("=================================");
//            Console.WriteLine($"TCP Server listening on port {port}");
//            Console.WriteLine("Waiting for client...");
//            Console.WriteLine("=================================");

//            LogReceived?.Invoke($"TCP Server listening on port {port}");
//            LogReceived?.Invoke("Waiting for client...");

//            _ = AcceptClientLoopAsync();
//        }
//        catch (Exception ex)
//        {
//            IsRunning = false;

//            Console.WriteLine($"TCP Server start error: {ex.Message}");
//            LogReceived?.Invoke($"Server start error: {ex.Message}");

//            throw;
//        }
//        finally
//        {
//            _startLock.Release();
//        }
//    }


//    // =========================================================
//    // ACCEPT CLIENT
//    // =========================================================

//    private async Task AcceptClientLoopAsync()
//    {
//        while (
//            IsRunning &&
//            _server != null)
//        {
//            TcpClient? client = null;

//            try
//            {
//                client =
//                    await _server
//                        .AcceptTcpClientAsync();

//                client.NoDelay = true;

//                lock (_lock)
//                {
//                    try
//                    {
//                        _stream?.Close();
//                    }
//                    catch
//                    {
//                    }

//                    try
//                    {
//                        _client?.Close();
//                    }
//                    catch
//                    {
//                    }

//                    _client = client;
//                    _stream =
//                        client.GetStream();
//                }

//                string endpoint =
//                    client.Client
//                        .RemoteEndPoint?
//                        .ToString()
//                    ?? "Unknown";

//                Console.WriteLine();
//                Console.WriteLine(
//                    "=================================");

//                Console.WriteLine(
//                    "TCP CLIENT CONNECTED");

//                Console.WriteLine(
//                    $"Client: {endpoint}");

//                Console.WriteLine(
//                    "=================================");

//                LogReceived?.Invoke(
//                    $"Client connected: {endpoint}");

//                await ReceiveLoopAsync(
//                    client);
//            }
//            catch (ObjectDisposedException)
//            {
//                break;
//            }
//            catch (SocketException ex)
//            {
//                if (!IsRunning)
//                    break;

//                Console.WriteLine(
//                    $"TCP socket error: {ex.Message}");

//                LogReceived?.Invoke(
//                    $"TCP socket error: {ex.Message}");
//            }
//            catch (Exception ex)
//            {
//                if (IsRunning)
//                {
//                    Console.WriteLine(
//                        $"TCP error: {ex.Message}");

//                    LogReceived?.Invoke(
//                        $"TCP error: {ex.Message}");
//                }
//            }
//            finally
//            {
//                if (client != null)
//                {
//                    lock (_lock)
//                    {
//                        if (_client == client)
//                        {
//                            _client = null;
//                            _stream = null;
//                        }
//                    }

//                    try
//                    {
//                        client.Close();
//                    }
//                    catch
//                    {
//                    }
//                }

//                if (IsRunning)
//                {
//                    Console.WriteLine();
//                    Console.WriteLine(
//                        "TCP client disconnected.");

//                    Console.WriteLine(
//                        "Waiting for client...");

//                    LogReceived?.Invoke(
//                        "Client disconnected.");

//                    LogReceived?.Invoke(
//                        "Waiting for client...");
//                }
//            }
//        }
//    }

//    // =========================================================
//    // RECEIVE
//    // =========================================================

//    private async Task ReceiveLoopAsync(
//        TcpClient client)
//    {

//        Console.WriteLine(
//            $"ReceiveLoop started: {client.Client.RemoteEndPoint}");

//        NetworkStream stream =
//            client.GetStream();

//        byte[] buffer =
//            new byte[8192];

//        try
//        {
//            while (
//                IsRunning &&
//                client.Connected)
//            {
//                int bytesRead =
//                    await stream.ReadAsync(
//                        buffer.AsMemory(
//                            0,
//                            buffer.Length));

//                if (bytesRead == 0)
//                    break;

//                byte[] data =
//                    buffer
//                        .Take(bytesRead)
//                        .ToArray();

//                string hex =
//                    Convert.ToHexString(data);

//                Console.WriteLine();
//                Console.WriteLine(
//                    $"TCP RX [{DateTime.Now:HH:mm:ss.fff}]");

//                Console.WriteLine(
//                    $"BYTES = {bytesRead}");

//                Console.WriteLine(
//                    $"HEX = {hex}");

//                LogReceived?.Invoke(
//                    $"RX [{DateTime.Now:HH:mm:ss.fff}] {bytesRead} bytes");

//                DataReceived?.Invoke(data);
//            }
//        }
//        catch (IOException ex)
//        {
//            if (IsRunning)
//            {
//                Console.WriteLine(
//                    $"TCP Receive error: {ex.Message}");

//                LogReceived?.Invoke(
//                    $"Receive error: {ex.Message}");
//            }
//        }
//        catch (ObjectDisposedException)
//        {
//        }
//        catch (Exception ex)
//        {
//            if (IsRunning)
//            {
//                Console.WriteLine(
//                    $"TCP Receive error: {ex.Message}");

//                LogReceived?.Invoke(
//                    $"TCP Receive error: {ex.Message}");
//            }
//        }
//    }

//    // =========================================================
//    // SEND RAW BYTES
//    // =========================================================

//    public async Task SendAsync(
//        byte[] data)
//    {
//        if (data == null ||
//            data.Length == 0)
//            return;

//        await _sendLock.WaitAsync();

//        try
//        {
//            NetworkStream? stream;

//            lock (_lock)
//            {
//                stream = _stream;
//            }

//            if (
//                stream == null ||
//                !IsClientConnected)
//            {
//                throw new InvalidOperationException(
//                    "TCP client is not connected.");
//            }

//            string hex =
//                Convert.ToHexString(data);

//            Console.WriteLine();
//            Console.WriteLine(
//                "========== TCP RAW TX ==========");

//            Console.WriteLine(
//                $"BYTES = {data.Length}");

//            Console.WriteLine(
//                $"HEX = {hex}");

//            Console.WriteLine(
//                $"CLIENT = {ClientEndPoint}");

//            Console.WriteLine(
//                "================================");

//            await stream.WriteAsync(
//                data.AsMemory(
//                    0,
//                    data.Length));

//            await stream.FlushAsync();

//            Console.WriteLine(
//                "TCP TX: SUCCESS");

//            LogReceived?.Invoke(
//                $"TX [{DateTime.Now:HH:mm:ss.fff}] {data.Length} bytes");
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine(
//                $"TCP TX ERROR: {ex.Message}");

//            LogReceived?.Invoke(
//                $"TCP TX ERROR: {ex.Message}");

//            throw;
//        }
//        finally
//        {
//            _sendLock.Release();
//        }
//    }

//    // =========================================================
//    // HEX
//    // =========================================================

//    public async Task SendHexAsync(
//        string hex)
//    {
//        if (string.IsNullOrWhiteSpace(hex))
//            return;

//        string cleaned =
//            hex
//                .Replace(" ", "")
//                .Replace("-", "")
//                .Replace("\r", "")
//                .Replace("\n", "");

//        byte[] data;

//        try
//        {
//            data =
//                Convert.FromHexString(
//                    cleaned);
//        }
//        catch (FormatException)
//        {
//            Console.WriteLine(
//                "Invalid HEX data.");

//            return;
//        }

//        await SendAsync(data);
//    }

//    // =========================================================
//    // STRING
//    // =========================================================

//    public async Task SendStringAsync(
//        string text)
//    {
//        if (string.IsNullOrEmpty(text))
//            return;

//        byte[] data =
//            System.Text.Encoding.ASCII
//                .GetBytes(text);

//        await SendAsync(data);
//    }

//    // =========================================================
//    // STOP
//    // =========================================================

//    public void Stop()
//    {
//        if (!IsRunning)
//            return;

//        IsRunning = false;

//        lock (_lock)
//        {
//            try
//            {
//                _stream?.Close();
//            }
//            catch
//            {
//            }

//            try
//            {
//                _client?.Close();
//            }
//            catch
//            {
//            }

//            try
//            {
//                _server?.Stop();
//            }
//            catch
//            {
//            }

//            _stream = null;
//            _client = null;
//            _server = null;
//        }

//        Console.WriteLine();
//        Console.WriteLine(
//            "=================================");

//        Console.WriteLine(
//            "TCP Server stopped.");

//        Console.WriteLine(
//            "=================================");

//        LogReceived?.Invoke(
//            "TCP Server stopped.");
//    }

//    public void Dispose()
//    {
//        Stop();

//        _sendLock.Dispose();
//    }
//}

using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace MyBlazorApp1.Services;

/// <summary>
/// TCP server that accepts multiple Android bridge clients concurrently.
/// The legacy single-client API remains available for the existing operator UI.
/// New per-device code should use ClientDataReceived and SendAsync(connectionId, data).
/// </summary>
public sealed class DiagnosticTcpServerService : IDisposable
{
    private sealed class ClientConnection
    {
        public readonly object ReceiveBufferLock = new();
        public readonly List<byte> ReceiveBuffer = new();
        public required string Id { get; init; }
        public required TcpClient Client { get; init; }
        public required NetworkStream Stream { get; init; }
        public required string EndPoint { get; init; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
    }
    private static readonly byte[] DiagnosticEndMarker =
    { 0xE2, 0x97, 0x89 };

    private const int MaxCachedReceiveBytes = 128 * 1024;

    public byte[] TakeBufferedData(string connectionId)
    {
        ClientConnection? connection;

        lock (_sync)
        {
            if (!_clients.TryGetValue(connectionId, out connection))
                return Array.Empty<byte>();
        }

        lock (connection.ReceiveBufferLock)
        {
            var data = connection.ReceiveBuffer.ToArray();
            connection.ReceiveBuffer.Clear();
            return data;
        }
    }

    private static void BufferIncomingData(
        ClientConnection connection,
        byte[] data)
    {
        lock (connection.ReceiveBufferLock)
        {
            if (connection.ReceiveBuffer.Count + data.Length >
                MaxCachedReceiveBytes)
            {
                connection.ReceiveBuffer.Clear();

                Console.WriteLine(
                    $"[TCP BUFFER] Cleared oversized cache: {connection.Id}");
            }

            if (data.Length <= MaxCachedReceiveBytes)
                connection.ReceiveBuffer.AddRange(data);
        }
    }
    public sealed record ConnectedClientInfo(
        string ConnectionId,
        string EndPoint,
        DateTime ConnectedAtUtc,
        bool IsPrimary);

    private readonly SemaphoreSlim _startLock = new(1, 1);
    private readonly object _sync = new();
    private readonly Dictionary<string, ClientConnection> _clients = new();

    private TcpListener? _server;
    private string? _primaryClientId;
    private int _port;

    public bool IsRunning { get; private set; }

    /// <summary>
    /// Compatibility property for the existing single-device operator UI.
    /// This represents only the legacy primary client, not all connected clients.
    /// </summary>
    public bool IsClientConnected
    {
        get
        {
            lock (_sync)
            {
                return _primaryClientId != null &&
                       _clients.TryGetValue(_primaryClientId, out var client) &&
                       client.Client.Connected;
            }
        }
    }

    public string? ClientEndPoint
    {
        get
        {
            lock (_sync)
            {
                return _primaryClientId != null &&
                       _clients.TryGetValue(_primaryClientId, out var client)
                    ? client.EndPoint
                    : null;
            }
        }
    }

    public string? PrimaryClientId
    {
        get
        {
            lock (_sync)
                return _primaryClientId;
        }
    }

    public int ConnectedClientCount
    {
        get
        {
            lock (_sync)
                return _clients.Count;
        }
    }

    /// <summary>Legacy event: receives data from the primary client only.</summary>
    public event Action<byte[]>? DataReceived;

    /// <summary>New event: receives data with the connection ID, from every client.</summary>
    public event Action<string, byte[]>? ClientDataReceived;

    public event Action<ConnectedClientInfo>? ClientConnected;
    public event Action<ConnectedClientInfo>? ClientDisconnected;
    public event Action<string>? LogReceived;

    public IReadOnlyList<ConnectedClientInfo> GetConnectedClients()
    {
        lock (_sync)
        {
            return _clients.Values
                .Select(c => new ConnectedClientInfo(
                    c.Id,
                    c.EndPoint,
                    _connectedAtUtc.TryGetValue(c.Id, out var connectedAt)
                        ? connectedAt
                        : DateTime.MinValue,
                    c.Id == _primaryClientId))
                .ToArray();
        }
    }

    private readonly Dictionary<string, DateTime> _connectedAtUtc = new();

    public async Task StartAsync(int port = 9000)
    {
        await _startLock.WaitAsync();
        try
        {
            if (IsRunning)
            {
                Log("TCP server is already running.");
                return;
            }

            var listener = new TcpListener(IPAddress.Any, port);
            try
            {
                listener.Start();
                _server = listener;
                _port = port;
                IsRunning = true;
            }
            catch
            {
                listener.Stop();
                throw;
            }

            Log($"TCP server listening on port {_port}; waiting for clients.");
            _ = AcceptClientLoopAsync(listener);
        }
        catch (Exception ex)
        {
            IsRunning = false;
            Log($"TCP server start error: {ex.Message}");
            throw;
        }
        finally
        {
            _startLock.Release();
        }
    }

    private async Task AcceptClientLoopAsync(TcpListener listener)
    {
        while (IsRunning)
        {
            TcpClient? tcpClient = null;
            try
            {
                tcpClient = await listener.AcceptTcpClientAsync();
                tcpClient.NoDelay = true;
                var stream = tcpClient.GetStream();
                var endpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                var id = Guid.NewGuid().ToString("N");
                var connection = new ClientConnection
                {
                    Id = id,
                    Client = tcpClient,
                    Stream = stream,
                    EndPoint = endpoint
                };

                ConnectedClientInfo info;
                lock (_sync)
                {
                    // The first client becomes the compatibility primary. We deliberately
                    // do not silently promote an unrelated remaining client if it disconnects.
                    if (_primaryClientId == null && _clients.Count == 0)
                        _primaryClientId = id;

                    _clients.Add(id, connection);
                    var connectedAt = DateTime.UtcNow;
                    _connectedAtUtc[id] = connectedAt;
                    info = new ConnectedClientInfo(
                        id, endpoint, connectedAt, id == _primaryClientId);
                }

                Log($"Client connected: {endpoint}; connectionId={id}; connected={ConnectedClientCount}");
                InvokeSafely(ClientConnected, info);

                // Do not await here: each client has its own receive loop.
                _ = ReceiveLoopAsync(connection);
                tcpClient = null; // Ownership transferred to ClientConnection.
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (IsRunning)
                    Log($"TCP accept socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                if (IsRunning)
                    Log($"TCP accept error: {ex.Message}");
            }
            finally
            {
                // Close only a client that failed before registration.
                tcpClient?.Close();
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientConnection connection)
    {
        var buffer = new byte[8192];
        try
        {
            while (IsRunning && connection.Client.Connected)
            {
                int bytesRead = await connection.Stream.ReadAsync(buffer.AsMemory());
                if (bytesRead == 0)
                    break;

                var data = buffer.AsSpan(0, bytesRead).ToArray();
                BufferIncomingData(connection, data);
                Log($"RX {bytesRead} bytes from {connection.EndPoint} (connectionId={connection.Id})");


                Log($"RX HEX from {connection.EndPoint}: {Convert.ToHexString(data)}");

                // Per-connection event is the only safe event for multi-device routing.
                InvokeSafely(ClientDataReceived, connection.Id, data);

                bool isPrimary;
                lock (_sync)
                    isPrimary = connection.Id == _primaryClientId;

                if (isPrimary)
                    InvokeSafely(DataReceived, data);
            }
        }
        catch (IOException ex)
        {
            if (IsRunning)
                Log($"Receive error from {connection.EndPoint}: {ex.Message}");
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException ex)
        {
            if (IsRunning)
                Log($"Receive socket error from {connection.EndPoint}: {ex.Message}");
        }
        catch (Exception ex)
        {
            if (IsRunning)
                Log($"Receive error from {connection.EndPoint}: {ex.Message}");
        }
        finally
        {
            RemoveClient(connection.Id);
        }
    }

    private void RemoveClient(string connectionId)
    {
        ClientConnection? removed = null;
        ConnectedClientInfo? info = null;

        lock (_sync)
        {
            if (_clients.Remove(connectionId, out removed))
            {
                var connectedAt = _connectedAtUtc.Remove(connectionId, out var time)
                    ? time
                    : DateTime.MinValue;
                info = new ConnectedClientInfo(
                    removed.Id,
                    removed.EndPoint,
                    connectedAt,
                    removed.Id == _primaryClientId);

                if (_primaryClientId == connectionId)
                    _primaryClientId = null;
            }
        }

        if (removed == null)
            return;

        try { removed.Stream.Close(); } catch { }
        try { removed.Client.Close(); } catch { }
        // Do not dispose SendLock here: a concurrent sender may still be unwinding
        // and must be able to release the semaphore safely.

        Log($"Client disconnected: {removed.EndPoint}; connectionId={connectionId}; connected={ConnectedClientCount}");
        if (info != null)
            InvokeSafely(ClientDisconnected, info);
    }

    /// <summary>Compatibility send: sends only to the legacy primary client.</summary>
    public Task SendAsync(byte[] data)
    {
        var primaryId = PrimaryClientId;
        if (primaryId == null)
            throw new InvalidOperationException("No primary TCP client is connected.");
        return SendAsync(primaryId, data);
    }

    /// <summary>Sends bytes to one specific TCP connection.</summary>
    public async Task SendAsync(string connectionId, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException("Connection ID is required.", nameof(connectionId));
        if (data == null || data.Length == 0)
            return;

        ClientConnection connection;
        lock (_sync)
        {
            if (!_clients.TryGetValue(connectionId, out connection!))
                throw new InvalidOperationException("The requested TCP client is not connected.");
        }

        await connection.SendLock.WaitAsync();
        try
        {
            // Ensure the same connection is still registered before writing.
            lock (_sync)
            {
                if (!_clients.TryGetValue(connectionId, out var current) ||
                    !ReferenceEquals(current, connection))
                    throw new InvalidOperationException("The requested TCP client has disconnected.");
            }

            await connection.Stream.WriteAsync(data.AsMemory());
            await connection.Stream.FlushAsync();
            Log($"TX {data.Length} bytes to {connection.EndPoint} (connectionId={connection.Id})");
        }
        finally
        {
            connection.SendLock.Release();
        }
    }

    public async Task SendHexAsync(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return;

        var cleaned = hex.Replace(" ", "").Replace("-", "").Replace("\r", "").Replace("\n", "");
        byte[] data;
        try { data = Convert.FromHexString(cleaned); }
        catch (FormatException)
        {
            Log("Invalid HEX data.");
            return;
        }
        await SendAsync(data);
    }

    public async Task SendStringAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        await SendAsync(System.Text.Encoding.ASCII.GetBytes(text));
    }

    public void Stop()
    {
        if (!IsRunning && _server == null)
            return;

        IsRunning = false;
        TcpListener? listener;
        ClientConnection[] clients;
        lock (_sync)
        {
            listener = _server;
            _server = null;
            clients = _clients.Values.ToArray();
            _clients.Clear();
            _connectedAtUtc.Clear();
            _primaryClientId = null;
        }

        try { listener?.Stop(); } catch { }
        foreach (var client in clients)
        {
            try { client.Stream.Close(); } catch { }
            try { client.Client.Close(); } catch { }
        }

        Log("TCP server stopped.");
    }

    private void Log(string message)
    {
        Console.WriteLine(message);
        InvokeSafely(LogReceived, message);
    }

    private static void InvokeSafely<T>(Action<T>? handlers, T arg)
    {
        if (handlers == null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
        {
            try { handler(arg); }
            catch (Exception ex) { Console.WriteLine($"TCP event handler error: {ex.Message}"); }
        }
    }

    private static void InvokeSafely<T1, T2>(Action<T1, T2>? handlers, T1 arg1, T2 arg2)
    {
        if (handlers == null) return;
        foreach (Action<T1, T2> handler in handlers.GetInvocationList())
        {
            try { handler(arg1, arg2); }
            catch (Exception ex) { Console.WriteLine($"TCP event handler error: {ex.Message}"); }
        }
    }


    public void Dispose()
    {
        Stop();
        _startLock.Dispose();
    }



    /// <summary>
    /// Sends a command to every currently connected TCP client.
    /// Each client is handled independently.
    /// </summary>
    public async Task<int> SendToAllClientsAsync(
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (data == null || data.Length == 0)
            throw new ArgumentException(
                "Command data cannot be empty.",
                nameof(data));

        // Take a snapshot so connections can change while sending.
        // Take a snapshot so connections can change while sending.
        var clients = GetConnectedClients();

        if (clients.Count == 0)
        {
            Log("Broadcast skipped: no TCP clients are connected.");
            return 0;
        }

        int succeeded = 0;

        foreach (var client in clients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await SendAsync(client.ConnectionId, data);

                succeeded++;

                Log(
                    $"Broadcast sent to {client.EndPoint}; " +
                    $"connectionId={client.ConnectionId}");
            }
            catch (Exception ex)
            {
                // One failed client must not prevent sending to the others.
                Log(
                    $"Broadcast failed for {client.EndPoint}; " +
                    $"connectionId={client.ConnectionId}; " +
                    $"error={ex.Message}");
            }
        }

        Log(
            $"Broadcast completed: {succeeded}/{clients.Count} clients.");

        return succeeded;
    }
}
