using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class UdpServer : IDisposable
{
    private UdpClient _udpClient;
    private readonly ConcurrentDictionary<IPEndPoint, DateTime> _knownClients = new ConcurrentDictionary<IPEndPoint, DateTime>();
    private CancellationTokenSource _cts;
    private Task _receiveTask;
    private bool _isRunning;

    public int Port { get; } = 50000;
    public IPAddress LocalIPv6Address { get; private set; }
    public IPEndPoint LocalEndPoint => new IPEndPoint(LocalIPv6Address, Port);

    // Events
    public event Action<IPEndPoint, string> OnTextMessageReceived;
    public event Action<IPEndPoint, byte[]> OnBinaryMessageReceived;
    public event Action<IPEndPoint> OnClientSeen; // First time or alive
    public event Action<Exception> OnError;

    public UdpServer()
    {
    }

    public void Start()
    {
        if (_isRunning) return;

        LocalIPv6Address = GetGlobalIPv6Address();

        if (LocalIPv6Address == null)
            throw new InvalidOperationException("No global IPv6 address found. Check your network connection and IPv6 support.");

        Console.WriteLine($"Detected public IPv6 address: {LocalIPv6Address}");
        Console.WriteLine($"Starting UDP server on port {Port} (IPv4 + IPv6 dual-mode)...");

        // Create IPv6 socket
        _udpClient = new UdpClient(AddressFamily.InterNetworkV6);

        // Enable dual-mode: accept IPv4 clients via IPv4-mapped IPv6 addresses (::ffff:192.168.x.x)
        _udpClient.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);

        // CRITICAL FIX: Bind to IPv6Any, NOT your specific address
        _udpClient.Client.Bind( new IPEndPoint(IPAddress.IPv6Any, Port));

        _cts = new CancellationTokenSource();
        _isRunning = true;

        _receiveTask = Task.Run(ReceiveLoopAsync, _cts.Token);

        Console.WriteLine($"UDP server successfully bound to [::]:{Port} (listening on all interfaces)");
        Console.WriteLine($"Clients can reach you via IPv6: [{LocalIPv6Address}]:{Port} or IPv4");
    }

    private async Task ReceiveLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                IPEndPoint remoteEP = result.RemoteEndPoint;

                // Remember this client (with timestamp for cleanup if needed)
                _knownClients[remoteEP] = DateTime.UtcNow;
                OnClientSeen?.Invoke(remoteEP);

                // Raise events
                string text = Encoding.UTF8.GetString(result.Buffer);
                OnTextMessageReceived?.Invoke(remoteEP, text);
                OnBinaryMessageReceived?.Invoke(remoteEP, result.Buffer);
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex)
            {
                if (!_cts.Token.IsCancellationRequested)
                    OnError?.Invoke(ex);
            }
        }
    }

    /// <summary>
    /// Send text to all known peers (everyone who sent us data)
    /// </summary>
    public async Task SendToAllAsync(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        await SendToAllAsync(data);
    }

    /// <summary>
    /// Send binary data to all known peers
    /// </summary>
    public async Task SendToAllAsync(byte[] data)
    {
        if (!_isRunning || data == null) return;

        var tasks = _knownClients.Keys.Select(endpoint =>
            _udpClient.SendAsync(data, data.Length, endpoint)
        );

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Send to a specific endpoint (useful for replies)
    /// </summary>
    public async Task SendAsync(string message, IPEndPoint target)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        await _udpClient.SendAsync(data, data.Length, target);
    }

    public async Task SendAsync(byte[] data, IPEndPoint target)
    {
        if (_isRunning)
            await _udpClient.SendAsync(data, data.Length, target);
    }

    /// <summary>
    /// Get first global (routable) IPv6 address
    /// </summary>
    private static IPAddress GetGlobalIPv6Address()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6)
                .Where(ip => !IPAddress.IsLoopback(ip))
                .Where(ip => !ip.IsIPv6LinkLocal)
                .Where(ip => !ip.IsIPv6SiteLocal)
                .OrderByDescending(ip => ip.IsIPv6Teredo ? 0 : 1)  // Prefer native over Teredo
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _cts?.Cancel();
        _udpClient?.Close();
        _udpClient?.Dispose();
        _knownClients.Clear();

        Console.WriteLine("UDP server stopped.");
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}