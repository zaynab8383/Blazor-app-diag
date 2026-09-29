using System.Text;
using System.Text.RegularExpressions;

namespace MyBlazorApp1.Services;

public class DiagnosticService : IDisposable
{
    private readonly DiagnosticTcpServerService _tcpServer;

    private readonly List<byte> _receiveBuffer = new();

    private readonly byte[] _messageEnd =
    {
        0xE2, 0x97, 0x89
    };

    public event Action<string>? RawDataReceived;
    public event Action<string>? MessageReceived;
    public event Action<string[]>? DiagnosticDataReceived;

    public bool IsRunning { get; private set; }

    public DiagnosticService(
        DiagnosticTcpServerService tcpServer)
    {
        _tcpServer = tcpServer;

        // =========================================
        // دریافت فقط از TCP
        // =========================================

        _tcpServer.DataReceived += OnTcpDataReceived;
    }

    // =========================================
    // TCP RECEIVE
    // =========================================

    private void OnTcpDataReceived(byte[] data)
    {
        if (!IsRunning)
            return;

        if (data == null || data.Length == 0)
            return;

        ProcessReceivedBytes(data);
    }

    // =========================================
    // COMMON RECEIVE PROCESSING
    // =========================================

    private void ProcessReceivedBytes(byte[] bytes)
    {
        try
        {
            // نمایش RAW HEX
            string hex =
                Convert.ToHexString(bytes)
                    .Replace("-", " ");

            RawDataReceived?.Invoke(hex);

            // اضافه کردن داده به بافر
            _receiveBuffer.AddRange(bytes);

            // پردازش پیام‌ها
            ProcessReceiveBuffer();
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Diagnostic receive processing error: {ex.Message}");
        }
    }

    // =========================================
    // MESSAGE BUFFER
    // =========================================

    private void ProcessReceiveBuffer()
    {
        while (true)
        {
            int endIndex = FindMessageEnd();

            // هنوز پیام کامل نشده
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
                Encoding.UTF8.GetString(messageBytes);

            MessageReceived?.Invoke(message);

            ProcessMessage(message);
        }
    }

    // =========================================
    // FIND ◆ DELIMITER
    // =========================================

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

    // =========================================
    // DIAGNOSTIC MESSAGE
    // =========================================

    private void ProcessMessage(string message)
    {
        Console.WriteLine();
        Console.WriteLine(
            "========== DIAGNOSTIC MESSAGE ==========");

        Console.WriteLine(message);

        Console.WriteLine(
            "=========================================");
        Console.WriteLine();

        string[] parts =
            ParseMessage(message);
        Console.WriteLine();
        Console.WriteLine("========== PARSED DATA ==========");

        for (int i = 0; i < parts.Length; i++)
        {
            Console.WriteLine($"[{i}] = [{parts[i]}]");
        }

        Console.WriteLine("=================================");




        if (parts.Length == 0)
            return;

        // مثال:
        //
        // ◆Type◆14◆Message◆...
        //
        // یا
        //
        // ◆Type◆20◆Count◆...
        //
        if (parts[0] == "Type")
        {
            DiagnosticDataReceived?.Invoke(parts);
        }
    }

    // =========================================
    // PARSE ◆...◆
    // =========================================

    private string[] ParseMessage(string message)
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

    // =========================================
    // START
    // =========================================

    public void Start()
    {
        IsRunning = true;

        Console.WriteLine(
            "DiagnosticService started.");
    }

    // =========================================
    // STOP
    // =========================================

    public void Stop()
    {
        IsRunning = false;

        _receiveBuffer.Clear();

        Console.WriteLine(
            "DiagnosticService stopped.");
    }

    // =========================================
    // SEND COMMAND THROUGH TCP
    // =========================================

    public async Task SendAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return;

        if (!_tcpServer.IsClientConnected)
        {
            throw new InvalidOperationException(
                "TCP client is not connected.");
        }

        byte[] data =
            Encoding.UTF8.GetBytes(
                command + "\n");

        await _tcpServer.SendAsync(data);

        Console.WriteLine(
            $"Diagnostic TX: {command}");
    }

    // =========================================
    // DISPOSE
    // =========================================

    public void Dispose()
    {
        _tcpServer.DataReceived -=
            OnTcpDataReceived;

        _receiveBuffer.Clear();
    }
}