//using System.Text;

//namespace MyBlazorApp1.Services;

//public sealed class OnlineDiagnosticDiscoveryService
//{
//    private readonly DiagnosticTcpServerService _tcpServer;

//    public OnlineDiagnosticDiscoveryService(
//        DiagnosticTcpServerService tcpServer)
//    {
//        _tcpServer = tcpServer;
//    }

//    /// <summary>
//    /// Sends the device-identification command to every
//    /// TCP connection that is currently registered.
//    /// </summary>
//    public Task<int> ProbeAllOnlineDevicesAsync(
//        CancellationToken cancellationToken = default)
//    {
//        // Keep the command framing consistent with the protocol.
//        // Confirm whether the diagnostic device expects LF or CRLF.
//        byte[] command = Encoding.UTF8.GetBytes("-11\n");

//        return _tcpServer.SendToAllClientsAsync(
//            command,
//            cancellationToken);
//    }

//    /// <summary>
//    /// Sends the identification command to one newly connected client.
//    /// </summary>
//    public Task ProbeConnectionAsync(
//        string connectionId,
//        CancellationToken cancellationToken = default)
//    {
//        cancellationToken.ThrowIfCancellationRequested();

//        byte[] command = Encoding.UTF8.GetBytes("-11\n");

//        return _tcpServer.SendAsync(connectionId, command);
//    }
//}


using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace MyBlazorApp1.Services;

public sealed record DiagnosticIdentity(
    string ConnectionId,
    string SoftwareVersion,
    string SerialNumber,
    string Counter);

public sealed class OnlineDiagnosticDiscoveryService
{
    private sealed class PendingProbe
    {
        public readonly object Sync = new();
        public readonly List<byte> Buffer = new();
        public readonly TaskCompletionSource<DiagnosticIdentity> Completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly ConcurrentDictionary<string, DiagnosticIdentity>
    _onlineDevices = new();
    private static readonly byte[] EndMarker = { 0xE2, 0x97, 0x89 };

    private readonly DiagnosticTcpServerService _tcpServer;

    private readonly ConcurrentDictionary<string, PendingProbe> _pending = new();

    public OnlineDiagnosticDiscoveryService(
        DiagnosticTcpServerService tcpServer)
    {
        _tcpServer = tcpServer;
        _tcpServer.ClientDataReceived += HandleClientDataReceived;
        _tcpServer.ClientDisconnected += HandleClientDisconnected;
    }

    // متد قبلی: ارسال فرمان به تمام کلاینت‌ها
    public Task<int> ProbeAllOnlineDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        byte[] command = Encoding.ASCII.GetBytes("-11\n");

        return _tcpServer.SendToAllClientsAsync(
            command, cancellationToken);
    }

    // متد قبلی: ارسال فرمان به یک اتصال
    public Task ProbeConnectionAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _tcpServer.SendAsync(
            connectionId,
            Encoding.ASCII.GetBytes("-11\n"));
    }

    // متد جدید: ارسال فرمان و انتظار برای پاسخ شناسایی
    public async Task<DiagnosticIdentity> ProbeConnectionAndReadIdentityAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var pending = new PendingProbe();

        if (!_pending.TryAdd(connectionId, pending))
        {
            throw new InvalidOperationException(
                "A diagnostic identification request is already pending for this connection.");
        }

        try
        {
            await _tcpServer.SendAsync(
                connectionId,
                Encoding.ASCII.GetBytes("-11\n"));

            return await pending.Completion.Task.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken);
        }
        finally
        {
            _pending.TryRemove(
                new KeyValuePair<string, PendingProbe>(
                    connectionId, pending));
        }
    }

    private void HandleClientDataReceived(
        string connectionId,
        byte[] data)
    {
        if (!_pending.TryGetValue(connectionId, out var pending))
            return;

        lock (pending.Sync)
        {
            pending.Buffer.AddRange(data);

            while (true)
            {
                int end = FindEndMarker(pending.Buffer);

                if (end < 0)
                    break;

                byte[] frame = pending.Buffer
                    .Take(end)
                    .ToArray();

                pending.Buffer.RemoveRange(0, end + EndMarker.Length);

                string message = Encoding.UTF8.GetString(frame);


                var match = Regex.Match(
                    message,
                    @"◆Type◆◆-11◆◆Software Ver\s*=\s*(.*?)◆◆Serial Number\s*=\s*(.*?)◆◆Counter\s*=\s*(.*?)◆",
                    RegexOptions.Singleline);


                if (!match.Success)
                    continue;

                var identity = new DiagnosticIdentity(
                    connectionId,
                    match.Groups[1].Value.Trim(),
                    match.Groups[2].Value.Trim(),
                    match.Groups[3].Value.Trim());

                _onlineDevices[connectionId] = identity;

                Console.WriteLine(
                    $"[DIAG IDENTITY] Connection={identity.ConnectionId}; " +
                    $"Software={identity.SoftwareVersion}; " +
                    $"Serial={identity.SerialNumber}; " +
                    $"Counter={identity.Counter}");

                pending.Completion.TrySetResult(identity);
                return;
            }

            // جلوگیری از رشد نامحدود بافر در صورت دریافت داده نامعتبر
            if (pending.Buffer.Count > 32768)
            {
                pending.Buffer.Clear();
                pending.Completion.TrySetException(
                    new InvalidDataException(
                        "Diagnostic response exceeded the maximum buffer size."));
            }
        }
    }



    //private void HandleClientDataReceived(
    //    string connectionId,
    //    byte[] data)
    //{
    //    if (!_pending.TryGetValue(connectionId, out var pending))
    //        return;

    //    lock (pending.Sync)
    //    {
    //        pending.Buffer.AddRange(data);

    //        while (true)
    //        {
    //            int end = FindEndMarker(pending.Buffer);

    //            if (end < 0)
    //                break;

    //            byte[] frame = pending.Buffer
    //                .Take(end)
    //                .ToArray();

    //            pending.Buffer.RemoveRange(
    //                0,
    //                end + EndMarker.Length);

    //            string message = Encoding.UTF8.GetString(frame);

    //            var match = Regex.Match(
    //                message,
    //                @"◆Type◆◆-11◆◆Software Ver\s*=\s*(.*?)◆◆Serial Number\s*=\s*(.*?)◆◆Counter\s*=\s*(.*?)◆",
    //                RegexOptions.Singleline);

    //            if (!match.Success)
    //                continue;

    //            string softwareVersion = match.Groups[1].Value.Trim();
    //            string serialNumber = match.Groups[2].Value.Trim();
    //            string counter = match.Groups[3].Value.Trim();

    //            // پاسخ ناقص را به‌عنوان هویت معتبر نپذیر.
    //            if (string.IsNullOrWhiteSpace(serialNumber))
    //            {
    //                Console.WriteLine(
    //                    $"[DIAG IDENTITY REJECTED] Connection={connectionId}; " +
    //                    "Serial Number is empty.");

    //                continue;
    //            }

    //            if (!long.TryParse(counter, out _))
    //            {
    //                Console.WriteLine(
    //                    $"[DIAG IDENTITY REJECTED] Connection={connectionId}; " +
    //                    $"Invalid Counter='{counter}'.");

    //                continue;
    //            }

    //            var identity = new DiagnosticIdentity(
    //                connectionId,
    //                softwareVersion,
    //                serialNumber,
    //                counter);

    //            _onlineDevices[connectionId] = identity;

    //            Console.WriteLine(
    //                $"[DIAG IDENTITY] Connection={identity.ConnectionId}; " +
    //                $"Software={identity.SoftwareVersion}; " +
    //                $"Serial={identity.SerialNumber}; " +
    //                $"Counter={identity.Counter}");

    //            pending.Completion.TrySetResult(identity);
    //            return;
    //        }

    //        // جلوگیری از رشد نامحدود بافر در صورت دریافت داده نامعتبر
    //        if (pending.Buffer.Count > 32768)
    //        {
    //            pending.Buffer.Clear();

    //            pending.Completion.TrySetException(
    //                new InvalidDataException(
    //                    "Diagnostic response exceeded the maximum buffer size."));
    //        }
    //    }
    //}


    private static int FindEndMarker(List<byte> buffer)
    {
        for (int i = 0; i <= buffer.Count - EndMarker.Length; i++)
        {
            if (buffer[i] == EndMarker[0] &&
                buffer[i + 1] == EndMarker[1] &&
                buffer[i + 2] == EndMarker[2])
            {
                return i;
            }
        }

        return -1;
    }

    //private void HandleClientDisconnected(
    //    DiagnosticTcpServerService.ConnectedClientInfo client)
    //{
    //    if (_pending.TryGetValue(client.ConnectionId, out var pending))
    //    {
    //        pending.Completion.TrySetException(
    //            new IOException(
    //                "Diagnostic client disconnected before returning its identity."));
    //    }
    //}


    private void HandleClientDisconnected(
    DiagnosticTcpServerService.ConnectedClientInfo client)
    {
        _onlineDevices.TryRemove(client.ConnectionId, out _);

        if (_pending.TryGetValue(client.ConnectionId, out var pending))
        {
            pending.Completion.TrySetException(
                new IOException(
                    "Diagnostic client disconnected before returning its identity."));
        }

        Console.WriteLine(
            $"[DIAG OFFLINE] Connection={client.ConnectionId}");
    }
    public IReadOnlyList<DiagnosticIdentity> GetOnlineDevices()
    {
        var connectedIds = _tcpServer.GetConnectedClients()
            .Select(client => client.ConnectionId)
            .ToHashSet(StringComparer.Ordinal);

        // فقط دستگاه‌هایی را برگردان که اتصال TCP آن‌ها هنوز برقرار است.
        foreach (var connectionId in _onlineDevices.Keys)
        {
            if (!connectedIds.Contains(connectionId))
            {
                _onlineDevices.TryRemove(connectionId, out _);
            }
        }

        return _onlineDevices.Values
            .OrderBy(device => device.SerialNumber)
            .ToArray();
    }
}
