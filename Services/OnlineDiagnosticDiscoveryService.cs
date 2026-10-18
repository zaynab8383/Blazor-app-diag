////using System.Text;

////namespace MyBlazorApp1.Services;

////public sealed class OnlineDiagnosticDiscoveryService
////{
////    private readonly DiagnosticTcpServerService _tcpServer;

////    public OnlineDiagnosticDiscoveryService(
////        DiagnosticTcpServerService tcpServer)
////    {
////        _tcpServer = tcpServer;
////    }

////    /// <summary>
////    /// Sends the device-identification command to every
////    /// TCP connection that is currently registered.
////    /// </summary>
////    public Task<int> ProbeAllOnlineDevicesAsync(
////        CancellationToken cancellationToken = default)
////    {
////        // Keep the command framing consistent with the protocol.
////        // Confirm whether the diagnostic device expects LF or CRLF.
////        byte[] command = Encoding.UTF8.GetBytes("-11\n");

////        return _tcpServer.SendToAllClientsAsync(
////            command,
////            cancellationToken);
////    }

////    /// <summary>
////    /// Sends the identification command to one newly connected client.
////    /// </summary>
////    public Task ProbeConnectionAsync(
////        string connectionId,
////        CancellationToken cancellationToken = default)
////    {
////        cancellationToken.ThrowIfCancellationRequested();

////        byte[] command = Encoding.UTF8.GetBytes("-11\n");

////        return _tcpServer.SendAsync(connectionId, command);
////    }
////}


//using System.Collections.Concurrent;
//using System.Text;
//using System.Text.RegularExpressions;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using MyBlazorApp1.Data;
//using MyBlazorApp1.Models;
////using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

//namespace MyBlazorApp1.Services;

//public sealed record DiagnosticIdentity(
//    string ConnectionId,
//    string SoftwareVersion,
//    string SerialNumber,
//    string Counter);

//public sealed class OnlineDiagnosticDiscoveryService
//{
//    private sealed class PendingProbe
//    {
//        public readonly object Sync = new();
//        public readonly List<byte> Buffer = new();
//        public readonly TaskCompletionSource<DiagnosticIdentity> Completion =
//            new(TaskCreationOptions.RunContinuationsAsynchronously);
//    }
//    private readonly IServiceScopeFactory _scopeFactory;
//    private readonly SemaphoreSlim _saveLock = new(1, 1);
//    private readonly ConcurrentDictionary<string, DiagnosticIdentity>
//    _onlineDevices = new();
//    //private static readonly byte[] EndMarker = { 0xE2, 0x97, 0x89 };
//    //private static readonly byte[] EndMarker = { 0xE2, 0x97, 0x86 };
//    private static readonly byte[] EndMarker =
//    {
//        0xE2, 0x97, 0x89 // ◉
//    };

//    private readonly DiagnosticTcpServerService _tcpServer;

//    private readonly ConcurrentDictionary<string, PendingProbe> _pending = new();

//    public OnlineDiagnosticDiscoveryService(
//        DiagnosticTcpServerService tcpServer,
//        IServiceScopeFactory scopeFactory)
//    {
//        _scopeFactory = scopeFactory;
//        _tcpServer = tcpServer;
//        _tcpServer.ClientDataReceived += HandleClientDataReceived;
//        _tcpServer.ClientDisconnected += HandleClientDisconnected;
//        _tcpServer.ClientConnected += HandleClientConnected;
//    }

//    // متد قبلی: ارسال فرمان به تمام کلاینت‌ها
//    public Task<int> ProbeAllOnlineDevicesAsync(
//        CancellationToken cancellationToken = default)
//    {
//        byte[] command = Encoding.ASCII.GetBytes("-11\n");

//        return _tcpServer.SendToAllClientsAsync(
//            command, cancellationToken);
//    }

//    // متد قبلی: ارسال فرمان به یک اتصال
//    public Task ProbeConnectionAsync(
//        string connectionId,
//        CancellationToken cancellationToken = default)
//    {
//        cancellationToken.ThrowIfCancellationRequested();

//        return _tcpServer.SendAsync(
//            connectionId,
//            Encoding.ASCII.GetBytes("-11\n"));
//    }


//    private async Task SaveDiscoveredDeviceAsync(
//    DiagnosticIdentity identity,
//    CancellationToken cancellationToken = default)
//    {
//        if (string.IsNullOrWhiteSpace(identity.SerialNumber))
//            throw new InvalidDataException(
//                "Cannot save a device with an empty serial number.");

//        await _saveLock.WaitAsync(cancellationToken);

//        try
//        {
//            using var scope = _scopeFactory.CreateScope();

//            var db = scope.ServiceProvider
//                .GetRequiredService<DiagDbContext>();

//            var now = DateTime.UtcNow;
//            var serial = identity.SerialNumber.Trim();

//            var device = await db.DiscoveredDiagnosticDevices
//                .SingleOrDefaultAsync(
//                    x => x.SerialNumber == serial,
//                    cancellationToken);

//            if (device == null)
//            {
//                device = new DiscoveredDiagnosticDevice
//                {
//                    SerialNumber = serial,
//                    SoftwareVersion = identity.SoftwareVersion,
//                    Counter = identity.Counter,
//                    FirstSeenAtUtc = now,
//                    LastSeenAtUtc = now
//                };

//                db.DiscoveredDiagnosticDevices.Add(device);
//            }
//            else
//            {
//                device.SoftwareVersion = identity.SoftwareVersion;
//                device.Counter = identity.Counter;
//                device.LastSeenAtUtc = now;
//            }

//            await db.SaveChangesAsync(cancellationToken);

//            Console.WriteLine(
//                $"[DIAG DB] Device saved; serial={serial}");
//        }
//        finally
//        {
//            _saveLock.Release();
//        }
//    }


//    // متد جدید: ارسال فرمان و انتظار برای پاسخ شناسایی
//    public async Task<DiagnosticIdentity> ProbeConnectionAndReadIdentityAsync(
//        string connectionId,
//        CancellationToken cancellationToken = default)
//    {
//        var pending = new PendingProbe();

//        if (!_pending.TryAdd(connectionId, pending))
//        {
//            throw new InvalidOperationException(
//                "A diagnostic identification request is already pending for this connection.");
//        }

//        try
//        {
//            await _tcpServer.SendAsync(
//                connectionId,
//                Encoding.ASCII.GetBytes("-11\n"));

//            return await pending.Completion.Task.WaitAsync(
//                TimeSpan.FromSeconds(10),
//                cancellationToken);
//        }
//        finally
//        {
//            _pending.TryRemove(
//                new KeyValuePair<string, PendingProbe>(
//                    connectionId, pending));
//        }
//    }

//    private void HandleClientConnected(
//        DiagnosticTcpServerService.ConnectedClientInfo client)
//    {
//        Console.WriteLine(
//            $"[DIAG PROBE] Starting identity probe: {client.ConnectionId}");

//        _ = ProbeNewConnectionAsync(client.ConnectionId);
//    }

//    //private async Task ProbeNewConnectionAsync(string connectionId)
//    //{
//    //    try
//    //    {
//    //        var identity = await ProbeConnectionAndReadIdentityAsync(
//    //            connectionId);

//    //        Console.WriteLine(
//    //            $"[DIAG DISCOVERY] Identified connection " +
//    //            $"{connectionId}; serial={identity.SerialNumber}");

//    //    }
//    //    catch (TimeoutException)
//    //    {
//    //        Console.WriteLine(
//    //            $"[DIAG DISCOVERY] Identity timeout: {connectionId}");
//    //    }
//    //    catch (OperationCanceledException)
//    //    {
//    //        Console.WriteLine(
//    //            $"[DIAG DISCOVERY] Identification cancelled: {connectionId}");
//    //    }
//    //    catch (Exception ex)
//    //    {
//    //        Console.WriteLine(
//    //            $"[DIAG DISCOVERY] Identification failed: " +
//    //            $"{connectionId}; error={ex.Message}");
//    //    }
//    //}
//    public DiagnosticIdentity? GetOnlineDeviceBySerialNumber(
//    string serialNumber)
//    {
//        if (string.IsNullOrWhiteSpace(serialNumber))
//            return null;

//        var connectedIds = _tcpServer.GetConnectedClients()
//            .Select(x => x.ConnectionId)
//            .ToHashSet(StringComparer.Ordinal);

//        foreach (var item in _onlineDevices.ToArray())
//        {
//            if (!connectedIds.Contains(item.Key))
//                _onlineDevices.TryRemove(item.Key, out _);
//        }

//        return _onlineDevices.Values.FirstOrDefault(x =>
//            string.Equals(
//                x.SerialNumber,
//                serialNumber.Trim(),
//                StringComparison.OrdinalIgnoreCase));
//    }
//    private async Task ProbeNewConnectionAsync(string connectionId)
//    {
//        try
//        {
//            var identity = await ProbeConnectionAndReadIdentityAsync(
//                connectionId);

//            Console.WriteLine(
//                $"[DIAG DISCOVERY] Identified connection " +
//                $"{connectionId}; serial={identity.SerialNumber}");

//            await SaveDiscoveredDeviceAsync(identity);
//        }
//        catch (TimeoutException)
//        {
//            Console.WriteLine(
//                $"[DIAG DISCOVERY] Identity timeout: {connectionId}");
//        }
//        catch (OperationCanceledException)
//        {
//            Console.WriteLine(
//                $"[DIAG DISCOVERY] Identification cancelled: {connectionId}");
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine(
//                $"[DIAG DISCOVERY] Identification or database save failed: " +
//                $"{connectionId}; error={ex.Message}");
//        }
//    }

//    private void HandleClientDataReceived(
//        string connectionId,
//        byte[] data)
//    {
//        Console.WriteLine(
//       $"[DIAG PARSER ENTER] Connection={connectionId}; Bytes={data.Length}");
//        //if (!_pending.TryGetValue(connectionId, out var pending))
//        //    return;
//        if (!_pending.TryGetValue(connectionId, out var pending))
//        {
//            Console.WriteLine(
//                $"[DIAG PARSER] No pending probe for connection={connectionId}");

//            return;
//        }

//        Console.WriteLine(
//            $"[DIAG PARSER] Pending probe found for connection={connectionId}");
//        lock (pending.Sync)
//        {
//            pending.Buffer.AddRange(data);

//            while (true)
//            {
//                int end = FindEndMarker(pending.Buffer);
//                Console.WriteLine(
//                    $"[DIAG PARSER] Buffer={pending.Buffer.Count}; " +
//                    $"EndMarkerIndex={end}");
//                if (end < 0)
//                    break;

//                byte[] frame = pending.Buffer
//                    .Take(end)
//                    .ToArray();

//                pending.Buffer.RemoveRange(0, end + EndMarker.Length);

//                string message = Encoding.UTF8.GetString(frame);
//                Console.WriteLine(
//                    $"[DIAG PARSER] Connection={connectionId}; " +
//                    $"Message=<{message}>");

//                //var match = Regex.Match(
//                //    message,
//                //    @"◆Type◆◆-11◆◆Software Ver\s*=\s*(.*?)◆◆Serial Number\s*=\s*(.*?)◆◆Counter\s*=\s*(.*?)◆",
//                //    RegexOptions.Singleline);
//                var match = Regex.Match(
//                    message,
//                    @"◆Type◆◆-11◆◆Software Ver\s*=\s*(.*?)◆◆Serial Number\s*=\s*(.*?)◆◆Counter\s*=\s*(.*?)(?:◆)?$",
//                    RegexOptions.Singleline);

//                Console.WriteLine(
//                     $"[DIAG PARSER] Regex success={match.Success}");

//                if (!match.Success)
//                    continue;

//                var identity = new DiagnosticIdentity(
//                    connectionId,
//                    match.Groups[1].Value.Trim(),
//                    match.Groups[2].Value.Trim(),
//                    match.Groups[3].Value.Trim());

//                _onlineDevices[connectionId] = identity;

//                Console.WriteLine(
//                    $"[DIAG IDENTITY] Connection={identity.ConnectionId}; " +
//                    $"Software={identity.SoftwareVersion}; " +
//                    $"Serial={identity.SerialNumber}; " +
//                    $"Counter={identity.Counter}");


//                pending.Completion.TrySetResult(identity);
//                return;
//            }

//            // جلوگیری از رشد نامحدود بافر در صورت دریافت داده نامعتبر
//            if (pending.Buffer.Count > 32768)
//            {
//                pending.Buffer.Clear();
//                pending.Completion.TrySetException(
//                    new InvalidDataException(
//                        "Diagnostic response exceeded the maximum buffer size."));
//            }
//        }
//    }



//    //private void HandleClientDataReceived(
//    //    string connectionId,
//    //    byte[] data)
//    //{
//    //    if (!_pending.TryGetValue(connectionId, out var pending))
//    //        return;

//    //    lock (pending.Sync)
//    //    {
//    //        pending.Buffer.AddRange(data);

//    //        while (true)
//    //        {
//    //            int end = FindEndMarker(pending.Buffer);

//    //            if (end < 0)
//    //                break;

//    //            byte[] frame = pending.Buffer
//    //                .Take(end)
//    //                .ToArray();

//    //            pending.Buffer.RemoveRange(
//    //                0,
//    //                end + EndMarker.Length);

//    //            string message = Encoding.UTF8.GetString(frame);

//    //            var match = Regex.Match(
//    //                message,
//    //                @"◆Type◆◆-11◆◆Software Ver\s*=\s*(.*?)◆◆Serial Number\s*=\s*(.*?)◆◆Counter\s*=\s*(.*?)◆",
//    //                RegexOptions.Singleline);

//    //            if (!match.Success)
//    //                continue;

//    //            string softwareVersion = match.Groups[1].Value.Trim();
//    //            string serialNumber = match.Groups[2].Value.Trim();
//    //            string counter = match.Groups[3].Value.Trim();

//    //            // پاسخ ناقص را به‌عنوان هویت معتبر نپذیر.
//    //            if (string.IsNullOrWhiteSpace(serialNumber))
//    //            {
//    //                Console.WriteLine(
//    //                    $"[DIAG IDENTITY REJECTED] Connection={connectionId}; " +
//    //                    "Serial Number is empty.");

//    //                continue;
//    //            }

//    //            if (!long.TryParse(counter, out _))
//    //            {
//    //                Console.WriteLine(
//    //                    $"[DIAG IDENTITY REJECTED] Connection={connectionId}; " +
//    //                    $"Invalid Counter='{counter}'.");

//    //                continue;
//    //            }

//    //            var identity = new DiagnosticIdentity(
//    //                connectionId,
//    //                softwareVersion,
//    //                serialNumber,
//    //                counter);

//    //            _onlineDevices[connectionId] = identity;

//    //            Console.WriteLine(
//    //                $"[DIAG IDENTITY] Connection={identity.ConnectionId}; " +
//    //                $"Software={identity.SoftwareVersion}; " +
//    //                $"Serial={identity.SerialNumber}; " +
//    //                $"Counter={identity.Counter}");

//    //            pending.Completion.TrySetResult(identity);
//    //            return;
//    //        }

//    //        // جلوگیری از رشد نامحدود بافر در صورت دریافت داده نامعتبر
//    //        if (pending.Buffer.Count > 32768)
//    //        {
//    //            pending.Buffer.Clear();

//    //            pending.Completion.TrySetException(
//    //                new InvalidDataException(
//    //                    "Diagnostic response exceeded the maximum buffer size."));
//    //        }
//    //    }
//    //}


//    private static int FindEndMarker(List<byte> buffer)
//    {
//        for (int i = 0; i <= buffer.Count - EndMarker.Length; i++)
//        {
//            if (buffer[i] == EndMarker[0] &&
//                buffer[i + 1] == EndMarker[1] &&
//                buffer[i + 2] == EndMarker[2])
//            {
//                return i;
//            }
//        }

//        return -1;
//    }

//    //private void HandleClientDisconnected(
//    //    DiagnosticTcpServerService.ConnectedClientInfo client)
//    //{
//    //    if (_pending.TryGetValue(client.ConnectionId, out var pending))
//    //    {
//    //        pending.Completion.TrySetException(
//    //            new IOException(
//    //                "Diagnostic client disconnected before returning its identity."));
//    //    }
//    //}


//    private void HandleClientDisconnected(
//    DiagnosticTcpServerService.ConnectedClientInfo client)
//    {
//        _onlineDevices.TryRemove(client.ConnectionId, out _);

//        if (_pending.TryGetValue(client.ConnectionId, out var pending))
//        {
//            pending.Completion.TrySetException(
//                new IOException(
//                    "Diagnostic client disconnected before returning its identity."));
//        }

//        Console.WriteLine(
//            $"[DIAG OFFLINE] Connection={client.ConnectionId}");
//    }
//    public IReadOnlyList<DiagnosticIdentity> GetOnlineDevices()
//    {
//        var connectedIds = _tcpServer.GetConnectedClients()
//            .Select(client => client.ConnectionId)
//            .ToHashSet(StringComparer.Ordinal);

//        // فقط دستگاه‌هایی را برگردان که اتصال TCP آن‌ها هنوز برقرار است.
//        foreach (var connectionId in _onlineDevices.Keys)
//        {
//            if (!connectedIds.Contains(connectionId))
//            {
//                _onlineDevices.TryRemove(connectionId, out _);
//            }
//        }

//        return _onlineDevices.Values
//            .OrderBy(device => device.SerialNumber)
//            .ToArray();
//    }
//}

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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyBlazorApp1.Data;
using MyBlazorApp1.Models;
//using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DiagnosticIdentity>
    _onlineDevices = new();
    //private static readonly byte[] EndMarker = { 0xE2, 0x97, 0x89 };
    //private static readonly byte[] EndMarker = { 0xE2, 0x97, 0x86 };
    private static readonly byte[] EndMarker =
    {
        0xE2, 0x97, 0x89 // ◉
    };

    private readonly DiagnosticTcpServerService _tcpServer;

    // Raised when a TCP connection has been matched to a stable diagnostic identity.
    public event Action<DiagnosticIdentity>? DeviceIdentified;

    private readonly ConcurrentDictionary<string, PendingProbe> _pending = new();

    public OnlineDiagnosticDiscoveryService(
        DiagnosticTcpServerService tcpServer,
        IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _tcpServer = tcpServer;
        _tcpServer.ClientDataReceived += HandleClientDataReceived;
        _tcpServer.ClientDisconnected += HandleClientDisconnected;
        _tcpServer.ClientConnected += HandleClientConnected;
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


    private async Task SaveDiscoveredDeviceAsync(
    DiagnosticIdentity identity,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identity.SerialNumber))
            throw new InvalidDataException(
                "Cannot save a device with an empty serial number.");

        await _saveLock.WaitAsync(cancellationToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<DiagDbContext>();

            var now = DateTime.UtcNow;
            var serial = identity.SerialNumber.Trim();

            var device = await db.DiscoveredDiagnosticDevices
                .SingleOrDefaultAsync(
                    x => x.SerialNumber == serial,
                    cancellationToken);

            if (device == null)
            {
                device = new DiscoveredDiagnosticDevice
                {
                    SerialNumber = serial,
                    SoftwareVersion = identity.SoftwareVersion,
                    Counter = identity.Counter,
                    FirstSeenAtUtc = now,
                    LastSeenAtUtc = now
                };

                db.DiscoveredDiagnosticDevices.Add(device);
            }
            else
            {
                device.SoftwareVersion = identity.SoftwareVersion;
                device.Counter = identity.Counter;
                device.LastSeenAtUtc = now;
            }

            await db.SaveChangesAsync(cancellationToken);

            Console.WriteLine(
                $"[DIAG DB] Device saved; serial={serial}");
        }
        finally
        {
            _saveLock.Release();
        }
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

    private void HandleClientConnected(
        DiagnosticTcpServerService.ConnectedClientInfo client)
    {
        Console.WriteLine(
            $"[DIAG PROBE] Starting identity probe: {client.ConnectionId}");

        _ = ProbeNewConnectionAsync(client.ConnectionId);
    }

    //private async Task ProbeNewConnectionAsync(string connectionId)
    //{
    //    try
    //    {
    //        var identity = await ProbeConnectionAndReadIdentityAsync(
    //            connectionId);

    //        Console.WriteLine(
    //            $"[DIAG DISCOVERY] Identified connection " +
    //            $"{connectionId}; serial={identity.SerialNumber}");

    //    }
    //    catch (TimeoutException)
    //    {
    //        Console.WriteLine(
    //            $"[DIAG DISCOVERY] Identity timeout: {connectionId}");
    //    }
    //    catch (OperationCanceledException)
    //    {
    //        Console.WriteLine(
    //            $"[DIAG DISCOVERY] Identification cancelled: {connectionId}");
    //    }
    //    catch (Exception ex)
    //    {
    //        Console.WriteLine(
    //            $"[DIAG DISCOVERY] Identification failed: " +
    //            $"{connectionId}; error={ex.Message}");
    //    }
    //}
    public DiagnosticIdentity? GetOnlineDeviceBySerialNumber(
    string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return null;

        var connectedIds = _tcpServer.GetConnectedClients()
            .Select(x => x.ConnectionId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in _onlineDevices.ToArray())
        {
            if (!connectedIds.Contains(item.Key))
                _onlineDevices.TryRemove(item.Key, out _);
        }

        return _onlineDevices.Values.FirstOrDefault(x =>
            string.Equals(
                x.SerialNumber,
                serialNumber.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }
    private async Task ProbeNewConnectionAsync(string connectionId)
    {
        try
        {
            var identity = await ProbeConnectionAndReadIdentityAsync(
                connectionId);

            Console.WriteLine(
                $"[DIAG DISCOVERY] Identified connection " +
                $"{connectionId}; serial={identity.SerialNumber}");

            await SaveDiscoveredDeviceAsync(identity);
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                $"[DIAG DISCOVERY] Identity timeout: {connectionId}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine(
                $"[DIAG DISCOVERY] Identification cancelled: {connectionId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[DIAG DISCOVERY] Identification or database save failed: " +
                $"{connectionId}; error={ex.Message}");
        }
    }

    private void HandleClientDataReceived(
        string connectionId,
        byte[] data)
    {
        Console.WriteLine(
       $"[DIAG PARSER ENTER] Connection={connectionId}; Bytes={data.Length}");
        //if (!_pending.TryGetValue(connectionId, out var pending))
        //    return;
        if (!_pending.TryGetValue(connectionId, out var pending))
        {
            Console.WriteLine(
                $"[DIAG PARSER] No pending probe for connection={connectionId}");

            return;
        }

        Console.WriteLine(
            $"[DIAG PARSER] Pending probe found for connection={connectionId}");
        lock (pending.Sync)
        {
            pending.Buffer.AddRange(data);

            while (true)
            {
                int end = FindEndMarker(pending.Buffer);
                Console.WriteLine(
                    $"[DIAG PARSER] Buffer={pending.Buffer.Count}; " +
                    $"EndMarkerIndex={end}");
                if (end < 0)
                    break;

                byte[] frame = pending.Buffer
                    .Take(end)
                    .ToArray();

                pending.Buffer.RemoveRange(0, end + EndMarker.Length);

                string message = Encoding.UTF8.GetString(frame);
                Console.WriteLine(
                    $"[DIAG PARSER] Connection={connectionId}; " +
                    $"Message=<{message}>");

                //var match = Regex.Match(
                //    message,
                //    @"◆Type◆◆-11◆◆Software Ver\s*=\s*(.*?)◆◆Serial Number\s*=\s*(.*?)◆◆Counter\s*=\s*(.*?)◆",
                //    RegexOptions.Singleline);
                var match = Regex.Match(
                    message,
                    @"◆Type◆◆-11◆◆Software Ver\s*=\s*(.*?)◆◆Serial Number\s*=\s*(.*?)◆◆Counter\s*=\s*(.*?)(?:◆)?$",
                    RegexOptions.Singleline);

                Console.WriteLine(
                     $"[DIAG PARSER] Regex success={match.Success}");

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
                NotifyDeviceIdentified(identity);
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


    private void NotifyDeviceIdentified(DiagnosticIdentity identity)
    {
        var handlers = DeviceIdentified;
        if (handlers is null)
            return;

        foreach (Action<DiagnosticIdentity> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(identity);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[DIAG IDENTITY] Subscriber failed for serial=" +
                    $"{identity.SerialNumber}: {ex.Message}");
            }
        }
    }

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
