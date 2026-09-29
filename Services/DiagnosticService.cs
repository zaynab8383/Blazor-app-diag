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

        _tcpServer.DataReceived += OnTcpDataReceived;
    }

    private void OnTcpDataReceived(byte[] data)
    {
        if (!IsRunning)
            return;

        string hexData = Convert.ToHexString(data)
            .Replace("-", " ");

        RawDataReceived?.Invoke(hexData);

        try
        {
            _receiveBuffer.AddRange(data);

            ProcessReceiveBuffer();
        }
        catch
        {
            // فعلاً خطای دریافت را نادیده می‌گیریم
        }
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

            MessageReceived?.Invoke(message);

            ProcessMessage(message);
        }
    }

    private int FindMessageEnd()
    {
        if (_receiveBuffer.Count < 3)
            return -1;

        for (int i = 0;
             i <= _receiveBuffer.Count - 3;
             i++)
        {
            if (_receiveBuffer[i] == 0xE2 &&
                _receiveBuffer[i + 1] == 0x97 &&
                _receiveBuffer[i + 2] == 0x89)
            {
                return i;
            }
        }

        return -1;
    }

    private void ProcessMessage(string message)
    {
        Console.WriteLine(
            "========== DIAGNOSTIC MESSAGE ==========");

        Console.WriteLine(message);

        Console.WriteLine(
            "=========================================");

        string[] parts = ParseMessage(message);

        if (parts.Length == 0)
            return;

        if (parts[0] == "Type")
        {
            DiagnosticDataReceived?.Invoke(parts);
        }
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

    public void Start()
    {
        IsRunning = true;

        _receiveBuffer.Clear();
    }

    public void Stop()
    {
        IsRunning = false;

        _receiveBuffer.Clear();
    }

    public async Task Send(string command)
    {
        if (!_tcpServer.IsClientConnected)
        {
            throw new InvalidOperationException(
                "TCP client is not connected.");
        }

        byte[] data =
            Encoding.UTF8.GetBytes(
                command + "\n");

        await _tcpServer.SendAsync(data);
    }

    public void Dispose()
    {
        _tcpServer.DataReceived -= OnTcpDataReceived;

        _receiveBuffer.Clear();
    }
}