using System.Text;
using System.Text.RegularExpressions;

namespace MyBlazorApp1.Services;

public class DiagnosticService : IDisposable
{
    private string? _selectedConnectionId;
    private readonly object _bufferLock = new();

    private readonly Dictionary<string, List<byte>> _pendingReceiveBuffers = new();

    private const int MaxPendingBytesPerConnection = 64 * 1024;

    public string? SelectedConnectionId => _selectedConnectionId;
    private readonly DiagnosticTcpServerService _tcpServer;

    private readonly List<byte> _receiveBuffer = new();

    private readonly byte[] _messageEnd =
    {
        0xE2,
        0x97,
        0x89
    };

    public event Action<string>? RawDataReceived;

    public event Action<string>? MessageReceived;

    public event Action<string[]>? DiagnosticDataReceived;

    public bool IsRunning { get; private set; }

    public DiagnosticService(
        DiagnosticTcpServerService tcpServer)
    {
        _tcpServer = tcpServer;

        _tcpServer.ClientDataReceived += OnTcpDataReceived;
    }

    // =========================================================
    // TCP RECEIVE
    // =========================================================

    public void SelectConnection(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException(
                "Connection ID is required.", nameof(connectionId));

        bool connected = _tcpServer.GetConnectedClients()
            .Any(c => c.ConnectionId == connectionId);

        if (!connected)
            throw new InvalidOperationException(
                "دیاگ انتخاب‌شده متصل نیست.");

        lock (_bufferLock)
        {
            _selectedConnectionId = connectionId;
            _receiveBuffer.Clear();
        }

        Console.WriteLine(
            $"[DIAG SELECT] Connection={connectionId}");
    }



    //public void SelectConnection(string connectionId)
    //{
    //    if (string.IsNullOrWhiteSpace(connectionId))
    //        throw new ArgumentException(
    //            "Connection ID is required.",
    //            nameof(connectionId));

    //    bool connected = _tcpServer.GetConnectedClients()
    //        .Any(c => c.ConnectionId == connectionId);

    //    if (!connected)
    //        throw new InvalidOperationException(
    //            "دیاگ انتخاب‌شده متصل نیست.");

    //    _receiveBuffer.Clear();
    //    _selectedConnectionId = connectionId;

    //    Console.WriteLine(
    //        $"[DIAG SELECT] Connection={_selectedConnectionId}; " +
    //        $"Running={IsRunning}; " +
    //        $"TCPConnected={connected}");
    //}


    //public void SelectConnection(string connectionId)
    //{
    //    if (string.IsNullOrWhiteSpace(connectionId))
    //        throw new ArgumentException("Connection ID is required.", nameof(connectionId));

    //    bool connected = _tcpServer.GetConnectedClients()
    //        .Any(c => c.ConnectionId == connectionId);

    //    if (!connected)
    //        throw new InvalidOperationException("دیاگ انتخاب‌شده متصل نیست.");

    //    _selectedConnectionId = connectionId;
    //    _receiveBuffer.Clear();
    //}

    //public void SelectConnection(string connectionId)
    //{
    //    if (string.IsNullOrWhiteSpace(connectionId))
    //        throw new ArgumentException("Connection ID is required.");

    //    if (!_tcpServer.GetConnectedClients()
    //        .Any(c => c.ConnectionId == connectionId))
    //    {
    //        throw new InvalidOperationException(
    //            "The selected diagnostic connection is no longer active.");
    //    }

    //    _selectedConnectionId = connectionId;
    //    _receiveBuffer.Clear();

    //    Console.WriteLine(
    //        $"Selected diagnostic connection: {connectionId}");
    //}
    //private void OnTcpDataReceived(byte[] data)
    //{
    //    if (!IsRunning)
    //        return;

    //    if (data == null || data.Length == 0)
    //        return;

    //    ProcessReceivedBytes(data);
    //}

    //private void OnTcpDataReceived(string connectionId, byte[] data)
    //{
    //    //Console.WriteLine(
    //    //    $"[CUSTOMER RX EVENT] connection={connectionId}, " +
    //    //    $"selected={_selectedConnectionId}, " +
    //    //    $"running={IsRunning}, bytes={data?.Length ?? 0}");
    //    if (!IsRunning)
    //        return;

    //    if (string.IsNullOrWhiteSpace(_selectedConnectionId) ||
    //        connectionId != _selectedConnectionId)
    //        return;

    //    if (data == null || data.Length == 0)
    //        return;

    //    ProcessReceivedBytes(data);
    //}


    private void OnTcpDataReceived(string connectionId, byte[] data)
    {
        if (string.IsNullOrWhiteSpace(connectionId) ||
            data == null ||
            data.Length == 0)
        {
            return;
        }

        lock (_bufferLock)
        {
            Console.WriteLine(
                $"[DIAG RX EVENT] Connection={connectionId}, " +
                $"Bytes={data?.Length ?? 0}, " +
                $"Running={IsRunning}, " +
                $"Selected={_selectedConnectionId ?? "null"}");
            // اگر این اتصال انتخاب شده و سرویس فعال است،
            // داده را مستقیماً پردازش می‌کنیم.
            if (IsRunning &&
                connectionId == _selectedConnectionId)
            {
                ProcessReceivedBytes(data);
                return;
            }

            // در غیر این صورت، داده را برای همین اتصال نگه می‌داریم.
            if (!_pendingReceiveBuffers.TryGetValue(
                    connectionId, out var buffer))
            {
                buffer = new List<byte>();
                _pendingReceiveBuffers[connectionId] = buffer;
            }

            // برای جلوگیری از رشد نامحدود حافظه.
            if (buffer.Count + data.Length > MaxPendingBytesPerConnection)
            {
                Console.WriteLine(
                    $"[DIAG BUFFER] Pending buffer limit reached: {connectionId}");

                buffer.Clear();

                // داده‌ای که به‌تنهایی از حد مجاز بزرگ‌تر است ذخیره نمی‌شود.
                if (data.Length > MaxPendingBytesPerConnection)
                    return;
            }

            buffer.AddRange(data);
        }
    }


    private void ProcessReceivedBytes(byte[] bytes)
    {
        try
        {
            string hex =
                Convert.ToHexString(bytes);

            Console.WriteLine();
            Console.WriteLine(
                "========== DIAGNOSTIC RAW RX ==========");

            Console.WriteLine(
                $"BYTES = {bytes.Length}");

            Console.WriteLine(
                $"HEX = {hex}");

            Console.WriteLine(
                "========================================");

            RawDataReceived?.Invoke(hex);

            _receiveBuffer.AddRange(bytes);

            ProcessReceiveBuffer();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Diagnostic receive processing error: {ex}");
        }
    }


    // =========================================================
    // MESSAGE BUFFER
    // =========================================================

    private void ProcessReceiveBuffer()
    {
        while (true)
        {
            int endIndex =
                FindMessageEnd();

            if (endIndex < 0)
                return;

            int messageLength =
                endIndex + _messageEnd.Length;

            byte[] messageBytes =
                _receiveBuffer
                    .Take(messageLength)
                    .ToArray();

            _receiveBuffer.RemoveRange(
                0,
                messageLength);

            string message =
                Encoding.UTF8.GetString(
                    messageBytes);

            Console.WriteLine();
            Console.WriteLine(
                "========== COMPLETE DIAGNOSTIC MESSAGE ==========");

            Console.WriteLine(message);

            Console.WriteLine(
                "==================================================");

            MessageReceived?.Invoke(message);

            ProcessMessage(message);
        }
    }


    private int FindMessageEnd()
    {
        if (_receiveBuffer.Count < 3)
            return -1;

        for (
            int i = 0;
            i <= _receiveBuffer.Count - 3;
            i++)
        {
            if (
                _receiveBuffer[i] == 0xE2 &&
                _receiveBuffer[i + 1] == 0x97 &&
                _receiveBuffer[i + 2] == 0x89)
            {
                return i;
            }
        }

        return -1;
    }


    // =========================================================
    // MESSAGE PARSER
    // =========================================================

    private void ProcessMessage(
        string message)
    {
        string[] parts =
            ParseMessage(message);

        Console.WriteLine(
            $"Parsed diagnostic fields: {parts.Length}");

        if (parts.Length == 0)
            return;

        if (parts.Length > 1)
        {
            Console.WriteLine(
                $"Diagnostic TYPE = [{parts[1]}]");
        }

        if (parts[0] == "Type")
        {
            DiagnosticDataReceived?.Invoke(parts);
        }
    }


    private string[] ParseMessage(
        string message)
    {
        List<string> result = new();

        MatchCollection matches =
            Regex.Matches(
                message,
                @"◆([^◆]*)◆");

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                result.Add(
                    match.Groups[1].Value.Trim());
            }
        }

        return result.ToArray();
    }


    // =========================================================
    // TCP SEND - IMPORTANT
    // =========================================================

    public async Task SendAsync(
        string command)
    {
        //if (string.IsNullOrEmpty(command))
        //    return;

        if (command == null) return;


        var connectionId = _selectedConnectionId;

        if (string.IsNullOrWhiteSpace(connectionId))
        {
            throw new InvalidOperationException(
                "No diagnostic connection has been selected.");
        }

        if (!_tcpServer.GetConnectedClients()
            .Any(c => c.ConnectionId == connectionId))
        {
            throw new InvalidOperationException(
                "The selected diagnostic connection is disconnected.");
        }
        //if (!_tcpServer.IsClientConnected)
        //{
        //    throw new InvalidOperationException(
        //        "TCP client is not connected.");
        //}


        // =====================================================
        // IMPORTANT:
        //
        // Diagnostic device expects:
        //
        // 0   -> 30 0A
        // 1   -> 31 0A
        // 13  -> 31 33 0A
        // -1  -> 2D 31 0A
        //
        // =====================================================

        string commandWithLf =
            command + "\n";

        byte[] data =
            Encoding.UTF8.GetBytes(
                commandWithLf);


        Console.WriteLine();
        Console.WriteLine(
            "=================================");

        Console.WriteLine(
            "DIAGNOSTIC COMMAND");

        Console.WriteLine(
            $"COMMAND = [{command}]");

        Console.WriteLine(
            $"COMMAND WITH LF = [{commandWithLf}]");

        Console.WriteLine(
            $"HEX = {Convert.ToHexString(data)}");

        Console.WriteLine(
            $"BYTES = {data.Length}");

        Console.WriteLine(
            "=================================");


        //await _tcpServer.SendAsync(data);
        await _tcpServer.SendAsync(connectionId, data);

        Console.WriteLine(
            "DIAGNOSTIC COMMAND SENT SUCCESSFULLY");
    }


    // =========================================================
    // SEND STRING
    // =========================================================

    public async Task SendStringAsync(
        string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        await SendAsync(text);
    }


    // =========================================================
    // SEND RAW HEX
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

        if (string.IsNullOrWhiteSpace(cleaned))
            return;

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

        await _tcpServer.SendAsync(data);
    }


    // =========================================================
    // START
    // =========================================================

    //public void Start()
    //{
    //    IsRunning = true;

    //    Console.WriteLine(
    //        "DiagnosticService started.");
    //}

    public void Start()
    {
        byte[] pendingData = Array.Empty<byte>();

        lock (_bufferLock)
        {
            IsRunning = true;

            if (!string.IsNullOrWhiteSpace(_selectedConnectionId) &&
                _pendingReceiveBuffers.TryGetValue(
                    _selectedConnectionId, out var buffer))
            {
                pendingData = buffer.ToArray();
                _pendingReceiveBuffers.Remove(_selectedConnectionId);
            }
        }

        //Console.WriteLine("DiagnosticService started.");

        //if (pendingData.Length > 0)
        //{
        //    Console.WriteLine(
        //        $"[DIAG BUFFER] Processing {pendingData.Length} previously received bytes.");

        //    ProcessReceivedBytes(pendingData);
        //}
        Console.WriteLine("DiagnosticService started.");

        var connectionId = _selectedConnectionId;

        if (!string.IsNullOrWhiteSpace(connectionId))
        {
            var bufferedData = _tcpServer.TakeBufferedData(connectionId);

            if (bufferedData.Length > 0)
            {
                Console.WriteLine(
                    $"[DIAG BUFFER] Processing {bufferedData.Length} cached TCP bytes.");

                ProcessReceivedBytes(bufferedData);
            }
        }
    }


    // =========================================================
    // STOP
    // =========================================================

    //public void Stop()
    //{
    //    IsRunning = false;

    //    _receiveBuffer.Clear();

    //    Console.WriteLine(
    //        "DiagnosticService stopped.");
    //}


    public void Stop()
    {
        lock (_bufferLock)
        {
            IsRunning = false;
            _receiveBuffer.Clear();
        }

        Console.WriteLine("DiagnosticService stopped.");
    }


    // =========================================================
    // DISPOSE
    // =========================================================

    public void Dispose()
    {
        _tcpServer.ClientDataReceived -= OnTcpDataReceived;

        Stop();
    }
}