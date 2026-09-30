using System.Net;
using System.Net.Sockets;

namespace MyBlazorApp1.Services;

public class DiagnosticTcpServerService : IDisposable
{
    private TcpListener? _server;
    private TcpClient? _client;
    private NetworkStream? _stream;

    private readonly object _lock = new();

    private readonly SemaphoreSlim _sendLock =
        new(1, 1);

    public bool IsRunning { get; private set; }

    public bool IsClientConnected
    {
        get
        {
            lock (_lock)
            {
                return
                    _client != null &&
                    _client.Connected &&
                    _stream != null;
            }
        }
    }

    public string? ClientEndPoint
    {
        get
        {
            lock (_lock)
            {
                return _client?
                    .Client?
                    .RemoteEndPoint?
                    .ToString();
            }
        }
    }

    public event Action<byte[]>? DataReceived;

    public event Action<string>? LogReceived;

    // =========================================================
    // START
    // =========================================================

    public async Task StartAsync(
        int port = 9000)
    {
        if (IsRunning)
        {
            Console.WriteLine(
                "TCP Server is already running.");

            return;
        }

        try
        {
            _server =
                new TcpListener(
                    IPAddress.Any,
                    port);

            _server.Start();

            IsRunning = true;

            Console.WriteLine();
            Console.WriteLine(
                "=================================");

            Console.WriteLine(
                "       DIAGNOSTIC TCP SERVER");

            Console.WriteLine(
                "=================================");

            Console.WriteLine(
                $"TCP Server listening on port {port}");

            Console.WriteLine(
                "Waiting for client...");

            Console.WriteLine(
                "=================================");

            LogReceived?.Invoke(
                $"TCP Server listening on port {port}");

            LogReceived?.Invoke(
                "Waiting for client...");

            _ = AcceptClientLoopAsync();
        }
        catch (Exception ex)
        {
            IsRunning = false;

            Console.WriteLine(
                $"TCP Server start error: {ex.Message}");

            LogReceived?.Invoke(
                $"Server start error: {ex.Message}");

            throw;
        }

        await Task.CompletedTask;
    }

    // =========================================================
    // ACCEPT CLIENT
    // =========================================================

    private async Task AcceptClientLoopAsync()
    {
        while (
            IsRunning &&
            _server != null)
        {
            TcpClient? client = null;

            try
            {
                client =
                    await _server
                        .AcceptTcpClientAsync();

                client.NoDelay = true;

                lock (_lock)
                {
                    try
                    {
                        _stream?.Close();
                    }
                    catch
                    {
                    }

                    try
                    {
                        _client?.Close();
                    }
                    catch
                    {
                    }

                    _client = client;
                    _stream =
                        client.GetStream();
                }

                string endpoint =
                    client.Client
                        .RemoteEndPoint?
                        .ToString()
                    ?? "Unknown";

                Console.WriteLine();
                Console.WriteLine(
                    "=================================");

                Console.WriteLine(
                    "TCP CLIENT CONNECTED");

                Console.WriteLine(
                    $"Client: {endpoint}");

                Console.WriteLine(
                    "=================================");

                LogReceived?.Invoke(
                    $"Client connected: {endpoint}");

                await ReceiveLoopAsync(
                    client);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException ex)
            {
                if (!IsRunning)
                    break;

                Console.WriteLine(
                    $"TCP socket error: {ex.Message}");

                LogReceived?.Invoke(
                    $"TCP socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                if (IsRunning)
                {
                    Console.WriteLine(
                        $"TCP error: {ex.Message}");

                    LogReceived?.Invoke(
                        $"TCP error: {ex.Message}");
                }
            }
            finally
            {
                if (client != null)
                {
                    lock (_lock)
                    {
                        if (_client == client)
                        {
                            _client = null;
                            _stream = null;
                        }
                    }

                    try
                    {
                        client.Close();
                    }
                    catch
                    {
                    }
                }

                if (IsRunning)
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        "TCP client disconnected.");

                    Console.WriteLine(
                        "Waiting for client...");

                    LogReceived?.Invoke(
                        "Client disconnected.");

                    LogReceived?.Invoke(
                        "Waiting for client...");
                }
            }
        }
    }

    // =========================================================
    // RECEIVE
    // =========================================================

    private async Task ReceiveLoopAsync(
        TcpClient client)
    {
        NetworkStream stream =
            client.GetStream();

        byte[] buffer =
            new byte[8192];

        try
        {
            while (
                IsRunning &&
                client.Connected)
            {
                int bytesRead =
                    await stream.ReadAsync(
                        buffer.AsMemory(
                            0,
                            buffer.Length));

                if (bytesRead == 0)
                    break;

                byte[] data =
                    buffer
                        .Take(bytesRead)
                        .ToArray();

                string hex =
                    Convert.ToHexString(data);

                Console.WriteLine();
                Console.WriteLine(
                    $"TCP RX [{DateTime.Now:HH:mm:ss.fff}]");

                Console.WriteLine(
                    $"BYTES = {bytesRead}");

                Console.WriteLine(
                    $"HEX = {hex}");

                LogReceived?.Invoke(
                    $"RX [{DateTime.Now:HH:mm:ss.fff}] {bytesRead} bytes");

                DataReceived?.Invoke(data);
            }
        }
        catch (IOException ex)
        {
            if (IsRunning)
            {
                Console.WriteLine(
                    $"TCP Receive error: {ex.Message}");

                LogReceived?.Invoke(
                    $"Receive error: {ex.Message}");
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            if (IsRunning)
            {
                Console.WriteLine(
                    $"TCP Receive error: {ex.Message}");

                LogReceived?.Invoke(
                    $"TCP Receive error: {ex.Message}");
            }
        }
    }

    // =========================================================
    // SEND RAW BYTES
    // =========================================================

    public async Task SendAsync(
        byte[] data)
    {
        if (data == null ||
            data.Length == 0)
            return;

        await _sendLock.WaitAsync();

        try
        {
            NetworkStream? stream;

            lock (_lock)
            {
                stream = _stream;
            }

            if (
                stream == null ||
                !IsClientConnected)
            {
                throw new InvalidOperationException(
                    "TCP client is not connected.");
            }

            string hex =
                Convert.ToHexString(data);

            Console.WriteLine();
            Console.WriteLine(
                "========== TCP RAW TX ==========");

            Console.WriteLine(
                $"BYTES = {data.Length}");

            Console.WriteLine(
                $"HEX = {hex}");

            Console.WriteLine(
                $"CLIENT = {ClientEndPoint}");

            Console.WriteLine(
                "================================");

            await stream.WriteAsync(
                data.AsMemory(
                    0,
                    data.Length));

            await stream.FlushAsync();

            Console.WriteLine(
                "TCP TX: SUCCESS");

            LogReceived?.Invoke(
                $"TX [{DateTime.Now:HH:mm:ss.fff}] {data.Length} bytes");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"TCP TX ERROR: {ex.Message}");

            LogReceived?.Invoke(
                $"TCP TX ERROR: {ex.Message}");

            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // =========================================================
    // HEX
    // =========================================================

    public async Task SendHexAsync(
        string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return;

        string cleaned =
            hex
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("\r", "")
                .Replace("\n", "");

        byte[] data;

        try
        {
            data =
                Convert.FromHexString(
                    cleaned);
        }
        catch (FormatException)
        {
            Console.WriteLine(
                "Invalid HEX data.");

            return;
        }

        await SendAsync(data);
    }

    // =========================================================
    // STRING
    // =========================================================

    public async Task SendStringAsync(
        string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        byte[] data =
            System.Text.Encoding.ASCII
                .GetBytes(text);

        await SendAsync(data);
    }

    // =========================================================
    // STOP
    // =========================================================

    public void Stop()
    {
        if (!IsRunning)
            return;

        IsRunning = false;

        lock (_lock)
        {
            try
            {
                _stream?.Close();
            }
            catch
            {
            }

            try
            {
                _client?.Close();
            }
            catch
            {
            }

            try
            {
                _server?.Stop();
            }
            catch
            {
            }

            _stream = null;
            _client = null;
            _server = null;
        }

        Console.WriteLine();
        Console.WriteLine(
            "=================================");

        Console.WriteLine(
            "TCP Server stopped.");

        Console.WriteLine(
            "=================================");

        LogReceived?.Invoke(
            "TCP Server stopped.");
    }

    public void Dispose()
    {
        Stop();

        _sendLock.Dispose();
    }
}