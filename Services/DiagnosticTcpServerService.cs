using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MyBlazorApp1.Services;

public class DiagnosticTcpServerService : IDisposable
{
    private TcpListener? _server;
    private TcpClient? _client;
    private NetworkStream? _stream;

    private readonly object _lock = new();

    public bool IsRunning { get; private set; }

    public bool IsClientConnected
    {
        get
        {
            lock (_lock)
            {
                return _client?.Connected ?? false;
            }
        }
    }

    public string? ClientEndPoint
    {
        get
        {
            lock (_lock)
            {
                return _client?.Client.RemoteEndPoint?.ToString();
            }
        }
    }

    // داده خام دریافتی از TCP
    public event Action<byte[]>? DataReceived;

    // لاگ‌های TCP
    public event Action<string>? LogReceived;


    // =========================================================
    // START SERVER
    // =========================================================

    public async Task StartAsync(int port = 9000)
    {
        if (IsRunning)
        {
            Console.WriteLine("TCP Server is already running.");
            return;
        }

        try
        {
            _server = new TcpListener(
                IPAddress.Any,
                port);

            _server.Start();

            IsRunning = true;

            Console.WriteLine("=================================");
            Console.WriteLine("       DIAGNOSTIC TCP SERVER");
            Console.WriteLine("=================================");
            Console.WriteLine(
                $"TCP Server listening on port {port}");
            Console.WriteLine("Waiting for client...");
            Console.WriteLine("=================================");

            LogReceived?.Invoke(
                $"TCP Server listening on port {port}");

            LogReceived?.Invoke(
                "Waiting for client...");

            // منتظر Client نمی‌مانیم.
            // Loop در background اجرا می‌شود.
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
                    await _server.AcceptTcpClientAsync();

                lock (_lock)
                {
                    // بستن Client قبلی
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
                    _stream = client.GetStream();
                }

                string endpoint =
                    client.Client.RemoteEndPoint?.ToString()
                    ?? "Unknown";

                Console.WriteLine();
                Console.WriteLine("=================================");
                Console.WriteLine("TCP CLIENT CONNECTED");
                Console.WriteLine($"Client: {endpoint}");
                Console.WriteLine("=================================");

                LogReceived?.Invoke(
                    $"Client connected: {endpoint}");

                await ReceiveLoopAsync(client);
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
    // RECEIVE TCP DATA
    // =========================================================

    private async Task ReceiveLoopAsync(
        TcpClient client)
    {
        NetworkStream stream =
            client.GetStream();

        byte[] buffer =
            new byte[4096];

        try
        {
            while (
                IsRunning &&
                client.Connected)
            {
                int bytesRead =
                    await stream.ReadAsync(buffer);

                if (bytesRead == 0)
                {
                    break;
                }

                byte[] data =
                    new byte[bytesRead];

                Array.Copy(
                    buffer,
                    0,
                    data,
                    0,
                    bytesRead);

                Console.WriteLine(
                    $"TCP RX [{DateTime.Now:HH:mm:ss.fff}] " +
                    $"{bytesRead} bytes");

                LogReceived?.Invoke(
                    $"RX [{DateTime.Now:HH:mm:ss.fff}] " +
                    $"{bytesRead} bytes");

                // =================================================
                // داده خام به DiagnosticService
                // =================================================

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
            // اتصال بسته شده است
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
    // SEND BYTES
    // =========================================================

    public async Task SendAsync(byte[] data)
    {
        if (data == null ||
            data.Length == 0)
        {
            return;
        }

        NetworkStream? stream;

        lock (_lock)
        {
            stream = _stream;
        }

        if (stream == null ||
            !IsClientConnected)
        {
            throw new InvalidOperationException(
                "TCP client is not connected.");
        }

        await stream.WriteAsync(data);

        await stream.FlushAsync();

        Console.WriteLine(
            $"TCP TX [{DateTime.Now:HH:mm:ss.fff}] " +
            $"{data.Length} bytes");

        LogReceived?.Invoke(
            $"TX [{DateTime.Now:HH:mm:ss.fff}] " +
            $"{data.Length} bytes");
    }


    // =========================================================
    // SEND STRING
    // =========================================================

    public async Task SendStringAsync(
        string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        byte[] data =
            Encoding.UTF8.GetBytes(text);

        await SendAsync(data);
    }


    // =========================================================
    // SEND HEX
    // =========================================================

    public async Task SendHexAsync(
        string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }

        string cleaned =
            hex
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("\r", "")
                .Replace("\n", "");

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return;
        }

        byte[] data;

        try
        {
            data =
                Convert.FromHexString(cleaned);
        }
        catch (FormatException)
        {
            Console.WriteLine(
                "Invalid HEX data.");

            LogReceived?.Invoke(
                "Invalid HEX data.");

            return;
        }

        await SendAsync(data);
    }


    // =========================================================
    // STOP SERVER
    // =========================================================

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

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
        Console.WriteLine("=================================");
        Console.WriteLine("TCP Server stopped.");
        Console.WriteLine("=================================");

        LogReceived?.Invoke(
            "TCP Server stopped.");
    }


    // =========================================================
    // DISPOSE
    // =========================================================

    public void Dispose()
    {
        Stop();
    }
}