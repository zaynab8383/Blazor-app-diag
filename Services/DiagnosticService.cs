using System.Text;
using System.Text.RegularExpressions;

namespace MyBlazorApp1.Services;

public class DiagnosticService : IDisposable
{
    private readonly DiagnosticTcpServerService _tcpServer;

    private readonly List<byte> _receiveBuffer = new();

    private readonly byte[] _messageEnd =
    {
        0xE2, 0x97, 0x89 // ◉
    };

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public bool IsRunning { get; private set; }

    public event Action<string>? RawDataReceived;
    public event Action<string>? MessageReceived;
    public event Action<string[]>? DiagnosticDataReceived;

    public DiagnosticService(
        DiagnosticTcpServerService tcpServer)
    {
        _tcpServer = tcpServer;

        _tcpServer.DataReceived += OnTcpDataReceived;
    }

    // =========================================================
    // TCP RECEIVE
    // =========================================================

    private void OnTcpDataReceived(byte[] data)
    {
        if (!IsRunning)
            return;

        if (data == null || data.Length == 0)
            return;

        try
        {
            ProcessReceivedBytes(data);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Diagnostic TCP processing ERROR: {ex}");
        }
    }

    private void ProcessReceivedBytes(byte[] bytes)
    {
        string hex =
            Convert.ToHexString(bytes);

        RawDataReceived?.Invoke(hex);

        _receiveBuffer.AddRange(bytes);

        ProcessReceiveBuffer();
    }

    private void ProcessReceiveBuffer()
    {
        while (true)
        {
            int endIndex = FindMessageEnd();

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
    // PARSE
    // =========================================================

    private void ProcessMessage(string message)
    {
        string[] parts =
            ParseMessage(message);

        Console.WriteLine(
            $"Parsed diagnostic fields: {parts.Length}");

        if (parts.Length == 0)
            return;

        if (!string.Equals(
                parts[0].Trim(),
                "Type",
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"Ignoring non-Type message. First field = [{parts[0]}]");

            return;
        }

        Console.WriteLine(
            $"Diagnostic TYPE = [{(parts.Length > 1 ? parts[1] : "")}]");

        DiagnosticDataReceived?.Invoke(parts);
    }

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

    // =========================================================
    // SEND
    // =========================================================

    public async Task SendAsync(string command)
    {
        if (string.IsNullOrEmpty(command))
            return;

        if (!_tcpServer.IsClientConnected)
            throw new InvalidOperationException(
                "TCP client is not connected.");

        // IMPORTANT:
        // The diagnostic device expects commands terminated by LF.
        //string commandWithNewLine = command + "\n";

        //byte[] data = Encoding.UTF8.GetBytes(commandWithNewLine);

        byte[] data = Encoding.UTF8.GetBytes(command);

        Console.WriteLine();
        Console.WriteLine("=================================");
        Console.WriteLine("DIAGNOSTIC COMMAND");
        Console.WriteLine($"COMMAND = [{command}]");
        //Console.WriteLine($"COMMAND WITH LF = [{commandWithNewLine}]");
        Console.WriteLine($"HEX = {Convert.ToHexString(data)}");
        Console.WriteLine("=================================");

        await _tcpServer.SendAsync(data);

        Console.WriteLine("DIAGNOSTIC COMMAND SENT SUCCESSFULLY");
    }


    public Task Send(string command)
    {
        return SendAsync(command);
    }

    // =========================================================
    // LIFECYCLE
    // =========================================================

    public void Start()
    {
        if (IsRunning)
            return;

        IsRunning = true;

        Console.WriteLine(
            "DiagnosticService started.");
    }

    public void Stop()
    {
        if (!IsRunning)
            return;

        IsRunning = false;

        _receiveBuffer.Clear();

        Console.WriteLine(
            "DiagnosticService stopped.");
    }

    public void Dispose()
    {
        _tcpServer.DataReceived -=
            OnTcpDataReceived;

        _sendLock.Dispose();

        _receiveBuffer.Clear();
    }
}