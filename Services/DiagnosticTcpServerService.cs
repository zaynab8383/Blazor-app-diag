using System.Net;
using System.Net.Sockets;

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

    // هر بار TCP داده‌ای دریافت کند
    public event Action<byte[]>? DataReceived;

    // برای نمایش لاگ در UI در صورت نیاز
    public event Action<string>? LogReceived;

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
            Console.WriteLine($"TCP Server listening on port {port}");
            Console.WriteLine("Waiting for client...");
            Console.WriteLine("=================================");

            LogReceived?.Invoke(
                $"TCP Server listening on port {port}");

            LogReceived?.Invoke(
                "Waiting for client...");

            // نباید StartAsync منتظر اتصال بماند
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

    private async Task AcceptClientLoopAsync()
    {
        while (IsRunning && _server != null)
        {
            TcpClient? client = null;

            try
            {
                client =
                    await _server.AcceptTcpClientAsync();

                lock (_lock)
                {
                    // اگر کلاینت قبلی هنوز وجود دارد
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
                    Console.WriteLine("TCP client disconnected.");
                    Console.WriteLine("Waiting for client...");

                    LogReceived?.Invoke(
                        "Client disconnected.");

                    LogReceived?.Invoke(
                        "Waiting for client...");
                }
            }
        }
    }

    private async Task ReceiveLoopAsync(
        TcpClient client)
    {
        NetworkStream stream =
            client.GetStream();

        byte[] buffer = new byte[4096];

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
                    data,
                    bytesRead);

                // فقط تعداد بایت‌ها را چاپ می‌کنیم
                Console.WriteLine(
                    $"TCP RX [{DateTime.Now:HH:mm:ss.fff}] {bytesRead} bytes");

                LogReceived?.Invoke(
                    $"RX [{DateTime.Now:HH:mm:ss.fff}] {bytesRead} bytes");

                // داده را برای DiagnosticService
                // یا هر پردازش‌کننده دیگری ارسال می‌کنیم
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
            // اتصال بسته شده
        }
        catch (Exception ex)
        {
            if (IsRunning)
            {
                Console.WriteLine(
                    $"TCP Receive error: {ex.Message}");

                LogReceived?.Invoke(
                    $"Receive error: {ex.Message}");
            }
        }
    }

    public async Task SendAsync(byte[] data)
    {
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
            $"TCP TX [{DateTime.Now:HH:mm:ss.fff}] {data.Length} bytes");

        LogReceived?.Invoke(
            $"TX [{DateTime.Now:HH:mm:ss.fff}] {data.Length} bytes");
    }

    public async Task SendStringAsync(
        string text)
    {
        if (text == null)
            return;

        byte[] data =
            System.Text.Encoding.UTF8.GetBytes(text);

        await SendAsync(data);
    }

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

        if (string.IsNullOrWhiteSpace(cleaned))
            return;

        byte[] data;

        try
        {
            data = Convert.FromHexString(cleaned);
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
        Console.WriteLine("=================================");
        Console.WriteLine("TCP Server stopped.");
        Console.WriteLine("=================================");

        LogReceived?.Invoke(
            "TCP Server stopped.");
    }

    public void Dispose()
    {
        Stop();
    }
}