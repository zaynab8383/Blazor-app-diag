//using System.Text;
//using System.Text.RegularExpressions;
//using System;
//using System.Collections.Generic;
//using System.Linq;

//namespace MyBlazorApp1.Services;

//public class DiagnosticService : IDisposable
//{
//    private string? _selectedConnectionId;
//    private readonly object _bufferLock = new();

//    private readonly Dictionary<string, List<byte>> _pendingReceiveBuffers = new();

//    private const int MaxPendingBytesPerConnection = 64 * 1024;

//    public string? SelectedConnectionId => _selectedConnectionId;
//    private readonly DiagnosticTcpServerService _tcpServer;

//    private readonly List<byte> _receiveBuffer = new();

//    private readonly byte[] _messageEnd =
//    {
//        0xE2,
//        0x97,
//        0x89
//    };

//    public event Action<string>? RawDataReceived;

//    public event Action<string>? MessageReceived;

//    public event Action<string[]>? DiagnosticDataReceived;

//    public bool IsRunning { get; private set; }

//    public DiagnosticService(
//        DiagnosticTcpServerService tcpServer)
//    {
//        _tcpServer = tcpServer;

//        _tcpServer.ClientDataReceived += OnTcpDataReceived;
//    }

//    // =========================================================
//    // TCP RECEIVE
//    // =========================================================

//    public void SelectConnection(string connectionId)
//    {
//        if (string.IsNullOrWhiteSpace(connectionId))
//            throw new ArgumentException(
//                "Connection ID is required.", nameof(connectionId));

//        bool connected = _tcpServer.GetConnectedClients()
//            .Any(c => c.ConnectionId == connectionId);

//        if (!connected)
//            throw new InvalidOperationException(
//                "دیاگ انتخاب‌شده متصل نیست.");

//        lock (_bufferLock)
//        {
//            _selectedConnectionId = connectionId;
//            _receiveBuffer.Clear();
//        }

//        Console.WriteLine(
//            $"[DIAG SELECT] Connection={connectionId}");
//    }



//    //public void SelectConnection(string connectionId)
//    //{
//    //    if (string.IsNullOrWhiteSpace(connectionId))
//    //        throw new ArgumentException(
//    //            "Connection ID is required.",
//    //            nameof(connectionId));

//    //    bool connected = _tcpServer.GetConnectedClients()
//    //        .Any(c => c.ConnectionId == connectionId);

//    //    if (!connected)
//    //        throw new InvalidOperationException(
//    //            "دیاگ انتخاب‌شده متصل نیست.");

//    //    _receiveBuffer.Clear();
//    //    _selectedConnectionId = connectionId;

//    //    Console.WriteLine(
//    //        $"[DIAG SELECT] Connection={_selectedConnectionId}; " +
//    //        $"Running={IsRunning}; " +
//    //        $"TCPConnected={connected}");
//    //}


//    //public void SelectConnection(string connectionId)
//    //{
//    //    if (string.IsNullOrWhiteSpace(connectionId))
//    //        throw new ArgumentException("Connection ID is required.", nameof(connectionId));

//    //    bool connected = _tcpServer.GetConnectedClients()
//    //        .Any(c => c.ConnectionId == connectionId);

//    //    if (!connected)
//    //        throw new InvalidOperationException("دیاگ انتخاب‌شده متصل نیست.");

//    //    _selectedConnectionId = connectionId;
//    //    _receiveBuffer.Clear();
//    //}

//    //public void SelectConnection(string connectionId)
//    //{
//    //    if (string.IsNullOrWhiteSpace(connectionId))
//    //        throw new ArgumentException("Connection ID is required.");

//    //    if (!_tcpServer.GetConnectedClients()
//    //        .Any(c => c.ConnectionId == connectionId))
//    //    {
//    //        throw new InvalidOperationException(
//    //            "The selected diagnostic connection is no longer active.");
//    //    }

//    //    _selectedConnectionId = connectionId;
//    //    _receiveBuffer.Clear();

//    //    Console.WriteLine(
//    //        $"Selected diagnostic connection: {connectionId}");
//    //}
//    //private void OnTcpDataReceived(byte[] data)
//    //{
//    //    if (!IsRunning)
//    //        return;

//    //    if (data == null || data.Length == 0)
//    //        return;

//    //    ProcessReceivedBytes(data);
//    //}

//    //private void OnTcpDataReceived(string connectionId, byte[] data)
//    //{
//    //    //Console.WriteLine(
//    //    //    $"[CUSTOMER RX EVENT] connection={connectionId}, " +
//    //    //    $"selected={_selectedConnectionId}, " +
//    //    //    $"running={IsRunning}, bytes={data?.Length ?? 0}");
//    //    if (!IsRunning)
//    //        return;

//    //    if (string.IsNullOrWhiteSpace(_selectedConnectionId) ||
//    //        connectionId != _selectedConnectionId)
//    //        return;

//    //    if (data == null || data.Length == 0)
//    //        return;

//    //    ProcessReceivedBytes(data);
//    //}


//    private void OnTcpDataReceived(string connectionId, byte[] data)
//    {
//        if (string.IsNullOrWhiteSpace(connectionId) ||
//            data == null ||
//            data.Length == 0)
//        {
//            return;
//        }

//        lock (_bufferLock)
//        {
//            Console.WriteLine(
//                $"[DIAG RX EVENT] Connection={connectionId}, " +
//                $"Bytes={data?.Length ?? 0}, " +
//                $"Running={IsRunning}, " +
//                $"Selected={_selectedConnectionId ?? "null"}");
//            // اگر این اتصال انتخاب شده و سرویس فعال است،
//            // داده را مستقیماً پردازش می‌کنیم.
//            if (IsRunning &&
//                connectionId == _selectedConnectionId)
//            {
//                ProcessReceivedBytes(data);
//                return;
//            }

//            // در غیر این صورت، داده را برای همین اتصال نگه می‌داریم.
//            if (!_pendingReceiveBuffers.TryGetValue(
//                    connectionId, out var buffer))
//            {
//                buffer = new List<byte>();
//                _pendingReceiveBuffers[connectionId] = buffer;
//            }

//            // برای جلوگیری از رشد نامحدود حافظه.
//            if (buffer.Count + data.Length > MaxPendingBytesPerConnection)
//            {
//                Console.WriteLine(
//                    $"[DIAG BUFFER] Pending buffer limit reached: {connectionId}");

//                buffer.Clear();

//                // داده‌ای که به‌تنهایی از حد مجاز بزرگ‌تر است ذخیره نمی‌شود.
//                if (data.Length > MaxPendingBytesPerConnection)
//                    return;
//            }

//            buffer.AddRange(data);
//        }
//    }


//    private void ProcessReceivedBytes(byte[] bytes)
//    {
//        try
//        {
//            string hex =
//                Convert.ToHexString(bytes);

//            Console.WriteLine();
//            Console.WriteLine(
//                "========== DIAGNOSTIC RAW RX ==========");

//            Console.WriteLine(
//                $"BYTES = {bytes.Length}");

//            Console.WriteLine(
//                $"HEX = {hex}");

//            Console.WriteLine(
//                "========================================");

//            RawDataReceived?.Invoke(hex);

//            _receiveBuffer.AddRange(bytes);

//            ProcessReceiveBuffer();
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine(
//                $"Diagnostic receive processing error: {ex}");
//        }
//    }


//    // =========================================================
//    // MESSAGE BUFFER
//    // =========================================================

//    private void ProcessReceiveBuffer()
//    {
//        while (true)
//        {
//            int endIndex =
//                FindMessageEnd();

//            if (endIndex < 0)
//                return;

//            int messageLength =
//                endIndex + _messageEnd.Length;

//            byte[] messageBytes =
//                _receiveBuffer
//                    .Take(messageLength)
//                    .ToArray();

//            _receiveBuffer.RemoveRange(
//                0,
//                messageLength);

//            string message =
//                Encoding.UTF8.GetString(
//                    messageBytes);

//            Console.WriteLine();
//            Console.WriteLine(
//                "========== COMPLETE DIAGNOSTIC MESSAGE ==========");

//            Console.WriteLine(message);

//            Console.WriteLine(
//                "==================================================");

//            MessageReceived?.Invoke(message);

//            ProcessMessage(message);
//        }
//    }


//    private int FindMessageEnd()
//    {
//        if (_receiveBuffer.Count < 3)
//            return -1;

//        for (
//            int i = 0;
//            i <= _receiveBuffer.Count - 3;
//            i++)
//        {
//            if (
//                _receiveBuffer[i] == 0xE2 &&
//                _receiveBuffer[i + 1] == 0x97 &&
//                _receiveBuffer[i + 2] == 0x89)
//            {
//                return i;
//            }
//        }

//        return -1;
//    }


//    // =========================================================
//    // MESSAGE PARSER
//    // =========================================================

//    //private void ProcessMessage(
//    //    string message)
//    //{
//    //    string[] parts =
//    //        ParseMessage(message);

//    //    Console.WriteLine(
//    //        $"Parsed diagnostic fields: {parts.Length}");

//    //    if (parts.Length == 0)
//    //        return;

//    //    if (parts.Length > 1)
//    //    {
//    //        Console.WriteLine(
//    //            $"Diagnostic TYPE = [{parts[1]}]");
//    //    }

//    //    if (parts[0] == "Type")
//    //    {
//    //        DiagnosticDataReceived?.Invoke(parts);
//    //    }
//    //}
//    //private void ProcessMessage(string message)
//    //{
//    //    string[] parts = ParseMessage(message);

//    //    for (int i = 0; i < parts.Length; i++)
//    //            {
//    //                Console.WriteLine($"[PARSED FIELD {i}] [{parts[i]}]");
//    //            }

//    //    Console.WriteLine(
//    //        $"Parsed diagnostic fields: {parts.Length}");

//    //    if (parts.Length == 0)
//    //        return;

//    //    if (parts[0] == "Type")
//    //    {
//    //        Console.WriteLine(
//    //            $"Diagnostic TYPE = [{(parts.Length > 1 ? parts[1] : "")}]");

//    //        DiagnosticDataReceived?.Invoke(parts);
//    //        return;
//    //    }

//    //    // Handle menu responses that omit the Type field.
//    //    if (TryNormalizeUntypedMenu(parts, out var normalized))
//    //    {
//    //        Console.WriteLine(
//    //            "[DIAG PARSER] Untyped menu response normalized.");

//    //        DiagnosticDataReceived?.Invoke(normalized);
//    //        return;
//    //    }

//    //    Console.WriteLine(
//    //        "[DIAG PARSER] Message ignored: no recognized Type or menu structure.");
//    //}



//    //private void ProcessMessage(string message)
//    //{
//    //    string[] parts = ParseMessage(message);
//    //    for (int i = 0; i < parts.Length; i++)
//    //    {
//    //        Console.WriteLine($"[DIAG FIELD {i}] [{parts[i]}]");
//    //    }
//    //    Console.WriteLine(
//    //        $"Parsed diagnostic fields: {parts.Length}");

//    //    // فقط فیلدهای خالی ابتدا و انتهای پیام حذف می‌شوند.
//    //    // فیلدهای خالی بین داده‌ها باید حفظ شوند.
//    //    int start = 0;
//    //    int end = parts.Length - 1;

//    //    while (start <= end &&
//    //           string.IsNullOrWhiteSpace(parts[start]))
//    //    {
//    //        start++;
//    //    }

//    //    while (end >= start &&
//    //           string.IsNullOrWhiteSpace(parts[end]))
//    //    {
//    //        end--;
//    //    }

//    //    if (start > end)
//    //    {
//    //        Console.WriteLine("[DIAG PARSER] Empty message.");
//    //        return;
//    //    }

//    //    parts = parts.Skip(start).Take(end - start + 1).ToArray();

//    //    Console.WriteLine(
//    //        $"[DIAG PARSER] FIRST FIELD = [{parts[0]}]");

//    //    // پیام استاندارد: Type, 4, Header, ...
//    //    if (string.Equals(
//    //            parts[0]?.Trim(),
//    //            "Type",
//    //            StringComparison.OrdinalIgnoreCase))
//    //    {
//    //        string type = parts.Length > 1
//    //            ? parts[1]?.Trim() ?? ""
//    //            : "";

//    //        Console.WriteLine(
//    //            $"[DIAG PARSER] Standard message. TYPE = [{type}]");

//    //        DiagnosticDataReceived?.Invoke(parts);
//    //        return;
//    //    }

//    //    // پیام منو بدون Type
//    //    if (TryNormalizeUntypedMenu(parts, out var normalized))
//    //    {
//    //        Console.WriteLine(
//    //            "[DIAG PARSER] Untyped menu response normalized.");

//    //        DiagnosticDataReceived?.Invoke(normalized);
//    //        return;
//    //    }

//    //    Console.WriteLine(
//    //        "[DIAG PARSER] Message ignored: no recognized Type or menu structure.");
//    //}

//    private void ProcessMessage(string message)
//    {
//        string[] parts = ParseMessage(message);

//        Console.WriteLine();
//        Console.WriteLine("========== DIAG PARSER ==========");
//        Console.WriteLine($"Parsed fields = {parts.Length}");

//        for (int i = 0; i < parts.Length; i++)
//        {
//            Console.WriteLine($"FIELD[{i}] = [{parts[i]}]");
//        }

//        Console.WriteLine("=================================");

//        if (parts.Length == 0)
//        {
//            Console.WriteLine("[DIAG PARSER] Empty message.");
//            return;
//        }

//        // پیام استاندارد:
//        // Type, -11, Software Ver = ..., Serial Number = ..., Counter = ...
//        if (string.Equals(
//                parts[0],
//                "Type",
//                StringComparison.OrdinalIgnoreCase))
//        {
//            if (parts.Length < 2 ||
//                string.IsNullOrWhiteSpace(parts[1]))
//            {
//                Console.WriteLine(
//                    "[DIAG PARSER] Standard message has no type value.");
//                return;
//            }

//            string type = parts[1].Trim();

//            Console.WriteLine(
//                $"[DIAG PARSER] Standard message. TYPE = [{type}]");

//            DiagnosticDataReceived?.Invoke(parts);
//            return;
//        }

//        // فقط پیام‌های منوی بدون Type که ساختار شناخته‌شده دارند.
//        if (TryNormalizeUntypedMenu(parts, out var normalized))
//        {
//            Console.WriteLine(
//                "[DIAG PARSER] Untyped menu response normalized.");

//            DiagnosticDataReceived?.Invoke(normalized);
//            return;
//        }

//        Console.WriteLine(
//            "[DIAG PARSER] Message ignored: unrecognized structure.");
//    }


//    //private string[] ParseMessage(string message)
//    //{
//    //    if (string.IsNullOrEmpty(message))
//    //        return Array.Empty<string>();

//    //    var matches = Regex.Matches(message, @"◆([^◆]*)◆");
//    //    var result = new List<string>();

//    //    foreach (Match match in matches)
//    //    {
//    //        if (match.Groups.Count > 1)
//    //        {
//    //            string field = match.Groups[1].Value.Trim();

//    //            if (!string.IsNullOrWhiteSpace(field))
//    //                result.Add(field);
//    //        }
//    //    }

//    //    return result.ToArray();
//    //}


//    //private bool TryNormalizeUntypedMenu(
//    //    string[] parts,
//    //    out string[] normalized)
//    //{
//    //    normalized = Array.Empty<string>();

//    //    if (parts == null || parts.Length < 3)
//    //        return false;

//    //    int selectedKeyIndex = Array.FindIndex(
//    //        parts,
//    //        p => string.Equals(
//    //            p?.Trim(),
//    //            "Selected_Index",
//    //            StringComparison.OrdinalIgnoreCase));

//    //    // منوی بدون Selected_Index را حدس نمی‌زنیم.
//    //    if (selectedKeyIndex < 0 ||
//    //        selectedKeyIndex + 1 >= parts.Length ||
//    //        !int.TryParse(
//    //            parts[selectedKeyIndex + 1]?.Trim(),
//    //            out int selectedIndex))
//    //    {
//    //        return false;
//    //    }

//    //    int countKeyIndex = Array.FindIndex(
//    //        parts,
//    //        p => string.Equals(
//    //            p?.Trim(),
//    //            "Count",
//    //            StringComparison.OrdinalIgnoreCase));

//    //    int count;

//    //    // قالب اول: Count سپس مقدار آن
//    //    if (countKeyIndex >= 0)
//    //    {
//    //        if (countKeyIndex + 1 >= parts.Length ||
//    //            !int.TryParse(
//    //                parts[countKeyIndex + 1]?.Trim(),
//    //                out count))
//    //        {
//    //            return false;
//    //        }
//    //    }
//    //    // قالب دوم: عدد ابتدای پیام، مثلاً 8
//    //    else if (!int.TryParse(parts[0]?.Trim(), out count))
//    //    {
//    //        return false;
//    //    }

//    //    if (count < 0 || count > 200 ||
//    //        selectedIndex < 0 ||
//    //        (count > 0 && selectedIndex >= count))
//    //    {
//    //        return false;
//    //    }

//    //    var items = new SortedDictionary<int, string>();

//    //    for (int i = 0; i < parts.Length; i++)
//    //    {
//    //        string field = parts[i]?.Trim() ?? "";

//    //        if (field.Length < 2 ||
//    //            !field.StartsWith("R", StringComparison.OrdinalIgnoreCase) ||
//    //            !int.TryParse(field.Substring(1), out int itemIndex) ||
//    //            itemIndex < 0)
//    //        {
//    //            continue;
//    //        }

//    //        // در قالب فعلی، متن ممکن است در فیلد بعدی
//    //        // یا پس از یک فیلد خالی قرار داشته باشد.
//    //        string text = "";

//    //        if (i + 1 < parts.Length)
//    //            text = parts[i + 1]?.Trim() ?? "";

//    //        if (string.IsNullOrWhiteSpace(text) &&
//    //            i + 2 < parts.Length)
//    //        {
//    //            text = parts[i + 2]?.Trim() ?? "";
//    //        }

//    //        if (!string.IsNullOrWhiteSpace(text))
//    //            items[itemIndex] = text;
//    //    }

//    //    if (count > 0 && items.Count == 0)
//    //        return false;

//    //    // ساخت آرایه مطابق قرارداد فعلی ShowBoxPage
//    //    var result = new List<string>
//    //{
//    //    "Type", "4",
//    //    "Header", "",
//    //    "", "",
//    //    count.ToString(),
//    //    "Selected_Index",
//    //    selectedIndex.ToString()
//    //};

//    //    foreach (var item in items.Take(count))
//    //    {
//    //        result.Add($"R{item.Key}");
//    //        result.Add("");
//    //        result.Add(item.Value);
//    //    }

//    //    normalized = result.ToArray();

//    //    Console.WriteLine(
//    //        $"[DIAG MENU] Normalized: Count={count}, " +
//    //        $"Selected_Index={selectedIndex}, Items={items.Count}");

//    //    return true;
//    //}


//    private bool TryNormalizeUntypedMenu(
//        string[] parts,
//        out string[] normalized)
//    {
//        normalized = Array.Empty<string>();

//        int countIndex = Array.FindIndex(
//            parts,
//            p => string.Equals(
//                p?.Trim(), "Count",
//                StringComparison.OrdinalIgnoreCase));

//        int selectedIndexKey = Array.FindIndex(
//            parts,
//            p => string.Equals(
//                p?.Trim(), "Selected_Index",
//                StringComparison.OrdinalIgnoreCase));

//        // Both named fields are required; do not guess from arbitrary data.
//        if (countIndex < 0 ||
//            selectedIndexKey < 0 ||
//            countIndex + 1 >= parts.Length ||
//            selectedIndexKey + 1 >= parts.Length)
//        {
//            return false;
//        }

//        if (!int.TryParse(parts[countIndex + 1]?.Trim(), out int count) ||
//            !int.TryParse(parts[selectedIndexKey + 1]?.Trim(), out int selected))
//        {
//            return false;
//        }

//        if (count < 0 || count > 200)
//            return false;

//        var items = new List<(int Index, string Text)>();

//        for (int i = 0; i < parts.Length; i++)
//        {
//            string field = parts[i]?.Trim() ?? "";

//            if (!field.StartsWith(
//                    "R", StringComparison.OrdinalIgnoreCase) ||
//                !int.TryParse(field[1..], out int itemIndex))
//            {
//                continue;
//            }

//            // The observed protocol has Rn, an empty field, then the text.
//            string text = "";

//            if (i + 2 < parts.Length)
//                text = parts[i + 2]?.Trim() ?? "";

//            if (string.IsNullOrWhiteSpace(text) && i + 1 < parts.Length)
//                text = parts[i + 1]?.Trim() ?? "";

//            items.Add((itemIndex, text));
//        }

//        if (items.Count == 0 && count > 0)
//            return false;

//        // Build the index layout expected by ShowBoxPage:
//        // [6] = Count, [8] = Selected_Index, [9...] = menu rows.
//        var result = new List<string>
//    {
//        "Type", "4", "Header", "",
//        "", "", count.ToString(),
//        "Selected_Index", selected.ToString()
//    };

//        foreach (var item in items.OrderBy(x => x.Index).Take(count))
//        {
//            result.Add($"R{item.Index}");
//            result.Add("");
//            result.Add(item.Text);
//        }

//        normalized = result.ToArray();
//        return true;
//    }


//    //private bool TryNormalizeUntypedMenu(
//    //    string[] parts,
//    //    out string[] normalized)
//    //{
//    //    normalized = Array.Empty<string>();

//    //    if (parts == null || parts.Length < 4)
//    //        return false;

//    //    int countIndex = Array.FindIndex(
//    //        parts,
//    //        p => string.Equals(
//    //            p?.Trim(), "Count",
//    //            StringComparison.OrdinalIgnoreCase));

//    //    int selectedIndexKey = Array.FindIndex(
//    //        parts,
//    //        p => string.Equals(
//    //            p?.Trim(), "Selected_Index",
//    //            StringComparison.OrdinalIgnoreCase));

//    //    int count;

//    //    // قالب دارای Count
//    //    if (countIndex >= 0 &&
//    //        countIndex + 1 < parts.Length &&
//    //        int.TryParse(parts[countIndex + 1]?.Trim(), out int parsedCount))
//    //    {
//    //        count = parsedCount;
//    //    }
//    //    // قالب مشاهده‌شده: عدد ابتدای پیام، سپس Selected_Index
//    //    else if (selectedIndexKey >= 0 &&
//    //             int.TryParse(parts[0]?.Trim(), out int leadingCount))
//    //    {
//    //        count = leadingCount;
//    //    }
//    //    else
//    //    {
//    //        return false;
//    //    }

//    //    if (count < 0 || count > 200 ||
//    //        selectedIndexKey < 0 ||
//    //        selectedIndexKey + 1 >= parts.Length ||
//    //        !int.TryParse(
//    //            parts[selectedIndexKey + 1]?.Trim(),
//    //            out int selected))
//    //    {
//    //        return false;
//    //    }

//    //    var items = new List<(int Index, string Text)>();

//    //    for (int i = 0; i < parts.Length; i++)
//    //    {
//    //        string field = parts[i]?.Trim() ?? "";

//    //        if (field.Length < 2 ||
//    //            !field.StartsWith("R", StringComparison.OrdinalIgnoreCase) ||
//    //            !int.TryParse(field[1..], out int itemIndex))
//    //        {
//    //            continue;
//    //        }

//    //        if (itemIndex < 0)
//    //            continue;

//    //        // در آرایه استخراج‌شده، متن معمولاً فیلد بعد از Rn است.
//    //        string text = i + 1 < parts.Length
//    //            ? parts[i + 1]?.Trim() ?? ""
//    //            : "";

//    //        if (string.IsNullOrWhiteSpace(text))
//    //            continue;

//    //        items.Add((itemIndex, text));
//    //    }

//    //    if (count > 0 && items.Count == 0)
//    //        return false;

//    //    // تعداد گزینه‌های واقعی باید با count سازگار باشد.
//    //    var orderedItems = items
//    //        .GroupBy(x => x.Index)
//    //        .Select(g => g.First())
//    //        .OrderBy(x => x.Index)
//    //        .ToList();

//    //    if (orderedItems.Count > count)
//    //        orderedItems = orderedItems.Take(count).ToList();

//    //    var result = new List<string>
//    //{
//    //    "Type", "4",
//    //    "Header", "",
//    //    "", "",
//    //    count.ToString(),
//    //    "Selected_Index", selected.ToString()
//    //};

//    //    foreach (var item in orderedItems)
//    //    {
//    //        result.Add($"R{item.Index}");
//    //        result.Add("");
//    //        result.Add(item.Text);
//    //    }

//    //    normalized = result.ToArray();

//    //    Console.WriteLine(
//    //        $"[DIAG PARSER] Menu normalized. Count={count}, " +
//    //        $"Selected_Index={selected}, Items={orderedItems.Count}");

//    //    return true;
//    //}




//    //private string[] ParseMessage(string message)
//    //{
//    //    if (string.IsNullOrWhiteSpace(message))
//    //        return Array.Empty<string>();

//    //    // Preserve empty fields: ShowBoxPage expects the
//    //    // original protocol field positions to remain intact.
//    //    string[] parts = message
//    //        .Split('◆', StringSplitOptions.None)
//    //        .Select(part => part.Trim())
//    //        .ToArray();

//    //    // Remove the terminator only from the final field.
//    //    if (parts.Length > 0)
//    //    {
//    //        parts[^1] = parts[^1]
//    //            .Replace("◉", "")
//    //            .Trim();
//    //    }

//    //    // Remove only trailing empty fields, not empty fields
//    //    // between protocol delimiters.
//    //    int length = parts.Length;

//    //    while (length > 0 &&
//    //           string.IsNullOrWhiteSpace(parts[length - 1]))
//    //    {
//    //        length--;
//    //    }

//    //    return parts.Take(length).ToArray();
//    //}



//    private string[] ParseMessage(
//        string message)
//    {
//        List<string> result = new();

//        MatchCollection matches =
//            Regex.Matches(
//                message,
//                @"◆([^◆]*)◆");

//        foreach (Match match in matches)
//        {
//            if (match.Groups.Count > 1)
//            {
//                result.Add(
//                    match.Groups[1].Value.Trim());
//            }
//        }

//        return result.ToArray();
//    }


//    // =========================================================
//    // TCP SEND - IMPORTANT
//    // =========================================================

//    public async Task SendAsync(
//        string command)
//    {
//        //if (string.IsNullOrEmpty(command))
//        //    return;

//        if (command == null) return;


//        var connectionId = _selectedConnectionId;

//        if (string.IsNullOrWhiteSpace(connectionId))
//        {
//            throw new InvalidOperationException(
//                "No diagnostic connection has been selected.");
//        }

//        if (!_tcpServer.GetConnectedClients()
//            .Any(c => c.ConnectionId == connectionId))
//        {
//            throw new InvalidOperationException(
//                "The selected diagnostic connection is disconnected.");
//        }
//        //if (!_tcpServer.IsClientConnected)
//        //{
//        //    throw new InvalidOperationException(
//        //        "TCP client is not connected.");
//        //}


//        // =====================================================
//        // IMPORTANT:
//        //
//        // Diagnostic device expects:
//        //
//        // 0   -> 30 0A
//        // 1   -> 31 0A
//        // 13  -> 31 33 0A
//        // -1  -> 2D 31 0A
//        //
//        // =====================================================

//        string commandWithLf =
//            command + "\n";

//        byte[] data =
//            Encoding.UTF8.GetBytes(
//                commandWithLf);


//        Console.WriteLine();
//        Console.WriteLine(
//            "=================================");

//        Console.WriteLine(
//            "DIAGNOSTIC COMMAND");

//        Console.WriteLine(
//            $"COMMAND = [{command}]");

//        Console.WriteLine(
//            $"COMMAND WITH LF = [{commandWithLf}]");

//        Console.WriteLine(
//            $"HEX = {Convert.ToHexString(data)}");

//        Console.WriteLine(
//            $"BYTES = {data.Length}");

//        Console.WriteLine(
//            "=================================");


//        //await _tcpServer.SendAsync(data);
//        await _tcpServer.SendAsync(connectionId, data);

//        Console.WriteLine(
//            "DIAGNOSTIC COMMAND SENT SUCCESSFULLY");
//    }


//    // =========================================================
//    // SEND STRING
//    // =========================================================

//    public async Task SendStringAsync(
//        string text)
//    {
//        if (string.IsNullOrEmpty(text))
//            return;

//        await SendAsync(text);
//    }


//    // =========================================================
//    // SEND RAW HEX
//    // =========================================================

//    public async Task SendHexAsync(
//        string hex)
//    {
//        if (string.IsNullOrWhiteSpace(hex))
//            return;

//        string cleaned =
//            hex
//                .Replace(" ", "")
//                .Replace("-", "")
//                .Replace("\r", "")
//                .Replace("\n", "");

//        if (string.IsNullOrWhiteSpace(cleaned))
//            return;

//        byte[] data;

//        try
//        {
//            data =
//                Convert.FromHexString(
//                    cleaned);
//        }
//        catch (FormatException)
//        {
//            Console.WriteLine(
//                "Invalid HEX data.");

//            return;
//        }

//        await _tcpServer.SendAsync(data);
//    }


//    // =========================================================
//    // START
//    // =========================================================

//    //public void Start()
//    //{
//    //    IsRunning = true;

//    //    Console.WriteLine(
//    //        "DiagnosticService started.");
//    //}

//    public void Start()
//    {
//        byte[] pendingData = Array.Empty<byte>();

//        lock (_bufferLock)
//        {
//            IsRunning = true;

//            if (!string.IsNullOrWhiteSpace(_selectedConnectionId) &&
//                _pendingReceiveBuffers.TryGetValue(
//                    _selectedConnectionId, out var buffer))
//            {
//                pendingData = buffer.ToArray();
//                _pendingReceiveBuffers.Remove(_selectedConnectionId);
//            }
//        }

//        //Console.WriteLine("DiagnosticService started.");

//        //if (pendingData.Length > 0)
//        //{
//        //    Console.WriteLine(
//        //        $"[DIAG BUFFER] Processing {pendingData.Length} previously received bytes.");

//        //    ProcessReceivedBytes(pendingData);
//        //}
//        Console.WriteLine("DiagnosticService started.");

//        var connectionId = _selectedConnectionId;

//        if (!string.IsNullOrWhiteSpace(connectionId))
//        {
//            var bufferedData = _tcpServer.TakeBufferedData(connectionId);

//            if (bufferedData.Length > 0)
//            {
//                Console.WriteLine(
//                    $"[DIAG BUFFER] Processing {bufferedData.Length} cached TCP bytes.");

//                ProcessReceivedBytes(bufferedData);
//            }
//        }
//    }


//    // =========================================================
//    // STOP
//    // =========================================================

//    //public void Stop()
//    //{
//    //    IsRunning = false;

//    //    _receiveBuffer.Clear();

//    //    Console.WriteLine(
//    //        "DiagnosticService stopped.");
//    //}


//    public void Stop()
//    {
//        lock (_bufferLock)
//        {
//            IsRunning = false;
//            _receiveBuffer.Clear();
//        }

//        Console.WriteLine("DiagnosticService stopped.");
//    }


//    // =========================================================
//    // DISPOSE
//    // =========================================================

//    public void Dispose()
//    {
//        _tcpServer.ClientDataReceived -= OnTcpDataReceived;

//        Stop();
//    }
//}

using System.Text;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyBlazorApp1.Services;

public class DiagnosticService : IDisposable
{
    private string? _selectedConnectionId;
    private string? _selectedSerialNumber;
    private readonly object _bufferLock = new();

    private readonly Dictionary<string, List<byte>> _pendingReceiveBuffers = new();

    private const int MaxPendingBytesPerConnection = 64 * 1024;

    public string? SelectedConnectionId => _selectedConnectionId;
    private readonly DiagnosticTcpServerService _tcpServer;
    private readonly OnlineDiagnosticDiscoveryService _discovery;

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
        DiagnosticTcpServerService tcpServer,
        OnlineDiagnosticDiscoveryService discovery)
    {
        _tcpServer = tcpServer;
        _discovery = discovery;

        _tcpServer.ClientDataReceived += OnTcpDataReceived;
        _tcpServer.ClientDisconnected += OnTcpClientDisconnected;
        _discovery.DeviceIdentified += OnDeviceIdentified;
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

        var identity = _discovery.GetOnlineDevices()
            .FirstOrDefault(x => string.Equals(
                x.ConnectionId, connectionId, StringComparison.Ordinal));

        lock (_bufferLock)
        {
            _selectedConnectionId = connectionId;
            _selectedSerialNumber = identity?.SerialNumber;
            _receiveBuffer.Clear();
            _pendingReceiveBuffers.Remove(connectionId);
        }

        Console.WriteLine(
            $"[DIAG SELECT] Connection={connectionId}; " +
            $"Serial={_selectedSerialNumber ?? "unknown"}");
    }

    // Keep the selected diagnostic bound by its serial number, not by a TCP
    // connection ID that changes whenever the Android app reconnects.
    private void OnDeviceIdentified(DiagnosticIdentity identity)
    {
        if (string.IsNullOrWhiteSpace(identity.SerialNumber) ||
            string.IsNullOrWhiteSpace(identity.ConnectionId))
        {
            return;
        }

        lock (_bufferLock)
        {
            if (string.IsNullOrWhiteSpace(_selectedSerialNumber) ||
                !string.Equals(
                    _selectedSerialNumber,
                    identity.SerialNumber,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!_tcpServer.GetConnectedClients().Any(c =>
                    string.Equals(
                        c.ConnectionId,
                        identity.ConnectionId,
                        StringComparison.Ordinal)))
            {
                return;
            }

            var previousConnectionId = _selectedConnectionId;
            _selectedConnectionId = identity.ConnectionId;

            if (!string.Equals(
                    previousConnectionId,
                    identity.ConnectionId,
                    StringComparison.Ordinal))
            {
                _receiveBuffer.Clear();

                // Reinsert bytes received before the identity frame was complete.
                // The current TCP chunk will be processed after this event returns.
                if (_pendingReceiveBuffers.Remove(
                        identity.ConnectionId, out var pendingBytes) &&
                    pendingBytes.Count > 0)
                {
                    Console.WriteLine(
                        $"[DIAG REBIND] Restoring {pendingBytes.Count} buffered bytes; " +
                        $"serial={identity.SerialNumber}; " +
                        $"old={previousConnectionId ?? "null"}; " +
                        $"new={identity.ConnectionId}");

                    if (IsRunning)
                        ProcessReceivedBytes(pendingBytes.ToArray());
                    else
                        _pendingReceiveBuffers[identity.ConnectionId] = pendingBytes;
                }
                else
                {
                    Console.WriteLine(
                        $"[DIAG REBIND] serial={identity.SerialNumber}; " +
                        $"old={previousConnectionId ?? "null"}; " +
                        $"new={identity.ConnectionId}");
                }
            }
        }
    }

    private void OnTcpClientDisconnected(
        DiagnosticTcpServerService.ConnectedClientInfo client)
    {
        lock (_bufferLock)
        {
            _pendingReceiveBuffers.Remove(client.ConnectionId);

            if (!string.Equals(
                    _selectedConnectionId,
                    client.ConnectionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            // Keep the selected serial so its next TCP connection can be rebound.
            _selectedConnectionId = null;
            _receiveBuffer.Clear();

            Console.WriteLine(
                $"[DIAG DISCONNECTED] Selected serial=" +
                $"{_selectedSerialNumber ?? "unknown"}; " +
                $"connection={client.ConnectionId}");
        }
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

    //private void ProcessMessage(
    //    string message)
    //{
    //    string[] parts =
    //        ParseMessage(message);

    //    Console.WriteLine(
    //        $"Parsed diagnostic fields: {parts.Length}");

    //    if (parts.Length == 0)
    //        return;

    //    if (parts.Length > 1)
    //    {
    //        Console.WriteLine(
    //            $"Diagnostic TYPE = [{parts[1]}]");
    //    }

    //    if (parts[0] == "Type")
    //    {
    //        DiagnosticDataReceived?.Invoke(parts);
    //    }
    //}
    //private void ProcessMessage(string message)
    //{
    //    string[] parts = ParseMessage(message);

    //    for (int i = 0; i < parts.Length; i++)
    //            {
    //                Console.WriteLine($"[PARSED FIELD {i}] [{parts[i]}]");
    //            }

    //    Console.WriteLine(
    //        $"Parsed diagnostic fields: {parts.Length}");

    //    if (parts.Length == 0)
    //        return;

    //    if (parts[0] == "Type")
    //    {
    //        Console.WriteLine(
    //            $"Diagnostic TYPE = [{(parts.Length > 1 ? parts[1] : "")}]");

    //        DiagnosticDataReceived?.Invoke(parts);
    //        return;
    //    }

    //    // Handle menu responses that omit the Type field.
    //    if (TryNormalizeUntypedMenu(parts, out var normalized))
    //    {
    //        Console.WriteLine(
    //            "[DIAG PARSER] Untyped menu response normalized.");

    //        DiagnosticDataReceived?.Invoke(normalized);
    //        return;
    //    }

    //    Console.WriteLine(
    //        "[DIAG PARSER] Message ignored: no recognized Type or menu structure.");
    //}



    //private void ProcessMessage(string message)
    //{
    //    string[] parts = ParseMessage(message);
    //    for (int i = 0; i < parts.Length; i++)
    //    {
    //        Console.WriteLine($"[DIAG FIELD {i}] [{parts[i]}]");
    //    }
    //    Console.WriteLine(
    //        $"Parsed diagnostic fields: {parts.Length}");

    //    // فقط فیلدهای خالی ابتدا و انتهای پیام حذف می‌شوند.
    //    // فیلدهای خالی بین داده‌ها باید حفظ شوند.
    //    int start = 0;
    //    int end = parts.Length - 1;

    //    while (start <= end &&
    //           string.IsNullOrWhiteSpace(parts[start]))
    //    {
    //        start++;
    //    }

    //    while (end >= start &&
    //           string.IsNullOrWhiteSpace(parts[end]))
    //    {
    //        end--;
    //    }

    //    if (start > end)
    //    {
    //        Console.WriteLine("[DIAG PARSER] Empty message.");
    //        return;
    //    }

    //    parts = parts.Skip(start).Take(end - start + 1).ToArray();

    //    Console.WriteLine(
    //        $"[DIAG PARSER] FIRST FIELD = [{parts[0]}]");

    //    // پیام استاندارد: Type, 4, Header, ...
    //    if (string.Equals(
    //            parts[0]?.Trim(),
    //            "Type",
    //            StringComparison.OrdinalIgnoreCase))
    //    {
    //        string type = parts.Length > 1
    //            ? parts[1]?.Trim() ?? ""
    //            : "";

    //        Console.WriteLine(
    //            $"[DIAG PARSER] Standard message. TYPE = [{type}]");

    //        DiagnosticDataReceived?.Invoke(parts);
    //        return;
    //    }

    //    // پیام منو بدون Type
    //    if (TryNormalizeUntypedMenu(parts, out var normalized))
    //    {
    //        Console.WriteLine(
    //            "[DIAG PARSER] Untyped menu response normalized.");

    //        DiagnosticDataReceived?.Invoke(normalized);
    //        return;
    //    }

    //    Console.WriteLine(
    //        "[DIAG PARSER] Message ignored: no recognized Type or menu structure.");
    //}

    private void ProcessMessage(string message)
    {
        string[] parts = ParseMessage(message);

        Console.WriteLine();
        Console.WriteLine("========== DIAG PARSER ==========");
        Console.WriteLine($"Parsed fields = {parts.Length}");

        for (int i = 0; i < parts.Length; i++)
        {
            Console.WriteLine($"FIELD[{i}] = [{parts[i]}]");
        }

        Console.WriteLine("=================================");

        if (parts.Length == 0)
        {
            Console.WriteLine("[DIAG PARSER] Empty message.");
            return;
        }

        // پیام استاندارد:
        // Type, -11, Software Ver = ..., Serial Number = ..., Counter = ...
        if (string.Equals(
                parts[0],
                "Type",
                StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 2 ||
                string.IsNullOrWhiteSpace(parts[1]))
            {
                Console.WriteLine(
                    "[DIAG PARSER] Standard message has no type value.");
                return;
            }

            string type = parts[1].Trim();

            Console.WriteLine(
                $"[DIAG PARSER] Standard message. TYPE = [{type}]");

            DiagnosticDataReceived?.Invoke(parts);
            return;
        }

        // فقط پیام‌های منوی بدون Type که ساختار شناخته‌شده دارند.
        if (TryNormalizeUntypedMenu(parts, out var normalized))
        {
            Console.WriteLine(
                "[DIAG PARSER] Untyped menu response normalized.");

            DiagnosticDataReceived?.Invoke(normalized);
            return;
        }

        Console.WriteLine(
            "[DIAG PARSER] Message ignored: unrecognized structure.");
    }


    //private string[] ParseMessage(string message)
    //{
    //    if (string.IsNullOrEmpty(message))
    //        return Array.Empty<string>();

    //    var matches = Regex.Matches(message, @"◆([^◆]*)◆");
    //    var result = new List<string>();

    //    foreach (Match match in matches)
    //    {
    //        if (match.Groups.Count > 1)
    //        {
    //            string field = match.Groups[1].Value.Trim();

    //            if (!string.IsNullOrWhiteSpace(field))
    //                result.Add(field);
    //        }
    //    }

    //    return result.ToArray();
    //}


    //private bool TryNormalizeUntypedMenu(
    //    string[] parts,
    //    out string[] normalized)
    //{
    //    normalized = Array.Empty<string>();

    //    if (parts == null || parts.Length < 3)
    //        return false;

    //    int selectedKeyIndex = Array.FindIndex(
    //        parts,
    //        p => string.Equals(
    //            p?.Trim(),
    //            "Selected_Index",
    //            StringComparison.OrdinalIgnoreCase));

    //    // منوی بدون Selected_Index را حدس نمی‌زنیم.
    //    if (selectedKeyIndex < 0 ||
    //        selectedKeyIndex + 1 >= parts.Length ||
    //        !int.TryParse(
    //            parts[selectedKeyIndex + 1]?.Trim(),
    //            out int selectedIndex))
    //    {
    //        return false;
    //    }

    //    int countKeyIndex = Array.FindIndex(
    //        parts,
    //        p => string.Equals(
    //            p?.Trim(),
    //            "Count",
    //            StringComparison.OrdinalIgnoreCase));

    //    int count;

    //    // قالب اول: Count سپس مقدار آن
    //    if (countKeyIndex >= 0)
    //    {
    //        if (countKeyIndex + 1 >= parts.Length ||
    //            !int.TryParse(
    //                parts[countKeyIndex + 1]?.Trim(),
    //                out count))
    //        {
    //            return false;
    //        }
    //    }
    //    // قالب دوم: عدد ابتدای پیام، مثلاً 8
    //    else if (!int.TryParse(parts[0]?.Trim(), out count))
    //    {
    //        return false;
    //    }

    //    if (count < 0 || count > 200 ||
    //        selectedIndex < 0 ||
    //        (count > 0 && selectedIndex >= count))
    //    {
    //        return false;
    //    }

    //    var items = new SortedDictionary<int, string>();

    //    for (int i = 0; i < parts.Length; i++)
    //    {
    //        string field = parts[i]?.Trim() ?? "";

    //        if (field.Length < 2 ||
    //            !field.StartsWith("R", StringComparison.OrdinalIgnoreCase) ||
    //            !int.TryParse(field.Substring(1), out int itemIndex) ||
    //            itemIndex < 0)
    //        {
    //            continue;
    //        }

    //        // در قالب فعلی، متن ممکن است در فیلد بعدی
    //        // یا پس از یک فیلد خالی قرار داشته باشد.
    //        string text = "";

    //        if (i + 1 < parts.Length)
    //            text = parts[i + 1]?.Trim() ?? "";

    //        if (string.IsNullOrWhiteSpace(text) &&
    //            i + 2 < parts.Length)
    //        {
    //            text = parts[i + 2]?.Trim() ?? "";
    //        }

    //        if (!string.IsNullOrWhiteSpace(text))
    //            items[itemIndex] = text;
    //    }

    //    if (count > 0 && items.Count == 0)
    //        return false;

    //    // ساخت آرایه مطابق قرارداد فعلی ShowBoxPage
    //    var result = new List<string>
    //{
    //    "Type", "4",
    //    "Header", "",
    //    "", "",
    //    count.ToString(),
    //    "Selected_Index",
    //    selectedIndex.ToString()
    //};

    //    foreach (var item in items.Take(count))
    //    {
    //        result.Add($"R{item.Key}");
    //        result.Add("");
    //        result.Add(item.Value);
    //    }

    //    normalized = result.ToArray();

    //    Console.WriteLine(
    //        $"[DIAG MENU] Normalized: Count={count}, " +
    //        $"Selected_Index={selectedIndex}, Items={items.Count}");

    //    return true;
    //}


    private bool TryNormalizeUntypedMenu(
        string[] parts,
        out string[] normalized)
    {
        normalized = Array.Empty<string>();

        int countIndex = Array.FindIndex(
            parts,
            p => string.Equals(
                p?.Trim(), "Count",
                StringComparison.OrdinalIgnoreCase));

        int selectedIndexKey = Array.FindIndex(
            parts,
            p => string.Equals(
                p?.Trim(), "Selected_Index",
                StringComparison.OrdinalIgnoreCase));

        // Both named fields are required; do not guess from arbitrary data.
        if (countIndex < 0 ||
            selectedIndexKey < 0 ||
            countIndex + 1 >= parts.Length ||
            selectedIndexKey + 1 >= parts.Length)
        {
            return false;
        }

        if (!int.TryParse(parts[countIndex + 1]?.Trim(), out int count) ||
            !int.TryParse(parts[selectedIndexKey + 1]?.Trim(), out int selected))
        {
            return false;
        }

        if (count < 0 || count > 200)
            return false;

        var items = new List<(int Index, string Text)>();

        for (int i = 0; i < parts.Length; i++)
        {
            string field = parts[i]?.Trim() ?? "";

            if (!field.StartsWith(
                    "R", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(field[1..], out int itemIndex))
            {
                continue;
            }

            // The observed protocol has Rn, an empty field, then the text.
            string text = "";

            if (i + 2 < parts.Length)
                text = parts[i + 2]?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(text) && i + 1 < parts.Length)
                text = parts[i + 1]?.Trim() ?? "";

            items.Add((itemIndex, text));
        }

        if (items.Count == 0 && count > 0)
            return false;

        // Build the index layout expected by ShowBoxPage:
        // [6] = Count, [8] = Selected_Index, [9...] = menu rows.
        var result = new List<string>
    {
        "Type", "4", "Header", "",
        "", "", count.ToString(),
        "Selected_Index", selected.ToString()
    };

        foreach (var item in items.OrderBy(x => x.Index).Take(count))
        {
            result.Add($"R{item.Index}");
            result.Add("");
            result.Add(item.Text);
        }

        normalized = result.ToArray();
        return true;
    }


    //private bool TryNormalizeUntypedMenu(
    //    string[] parts,
    //    out string[] normalized)
    //{
    //    normalized = Array.Empty<string>();

    //    if (parts == null || parts.Length < 4)
    //        return false;

    //    int countIndex = Array.FindIndex(
    //        parts,
    //        p => string.Equals(
    //            p?.Trim(), "Count",
    //            StringComparison.OrdinalIgnoreCase));

    //    int selectedIndexKey = Array.FindIndex(
    //        parts,
    //        p => string.Equals(
    //            p?.Trim(), "Selected_Index",
    //            StringComparison.OrdinalIgnoreCase));

    //    int count;

    //    // قالب دارای Count
    //    if (countIndex >= 0 &&
    //        countIndex + 1 < parts.Length &&
    //        int.TryParse(parts[countIndex + 1]?.Trim(), out int parsedCount))
    //    {
    //        count = parsedCount;
    //    }
    //    // قالب مشاهده‌شده: عدد ابتدای پیام، سپس Selected_Index
    //    else if (selectedIndexKey >= 0 &&
    //             int.TryParse(parts[0]?.Trim(), out int leadingCount))
    //    {
    //        count = leadingCount;
    //    }
    //    else
    //    {
    //        return false;
    //    }

    //    if (count < 0 || count > 200 ||
    //        selectedIndexKey < 0 ||
    //        selectedIndexKey + 1 >= parts.Length ||
    //        !int.TryParse(
    //            parts[selectedIndexKey + 1]?.Trim(),
    //            out int selected))
    //    {
    //        return false;
    //    }

    //    var items = new List<(int Index, string Text)>();

    //    for (int i = 0; i < parts.Length; i++)
    //    {
    //        string field = parts[i]?.Trim() ?? "";

    //        if (field.Length < 2 ||
    //            !field.StartsWith("R", StringComparison.OrdinalIgnoreCase) ||
    //            !int.TryParse(field[1..], out int itemIndex))
    //        {
    //            continue;
    //        }

    //        if (itemIndex < 0)
    //            continue;

    //        // در آرایه استخراج‌شده، متن معمولاً فیلد بعد از Rn است.
    //        string text = i + 1 < parts.Length
    //            ? parts[i + 1]?.Trim() ?? ""
    //            : "";

    //        if (string.IsNullOrWhiteSpace(text))
    //            continue;

    //        items.Add((itemIndex, text));
    //    }

    //    if (count > 0 && items.Count == 0)
    //        return false;

    //    // تعداد گزینه‌های واقعی باید با count سازگار باشد.
    //    var orderedItems = items
    //        .GroupBy(x => x.Index)
    //        .Select(g => g.First())
    //        .OrderBy(x => x.Index)
    //        .ToList();

    //    if (orderedItems.Count > count)
    //        orderedItems = orderedItems.Take(count).ToList();

    //    var result = new List<string>
    //{
    //    "Type", "4",
    //    "Header", "",
    //    "", "",
    //    count.ToString(),
    //    "Selected_Index", selected.ToString()
    //};

    //    foreach (var item in orderedItems)
    //    {
    //        result.Add($"R{item.Index}");
    //        result.Add("");
    //        result.Add(item.Text);
    //    }

    //    normalized = result.ToArray();

    //    Console.WriteLine(
    //        $"[DIAG PARSER] Menu normalized. Count={count}, " +
    //        $"Selected_Index={selected}, Items={orderedItems.Count}");

    //    return true;
    //}




    //private string[] ParseMessage(string message)
    //{
    //    if (string.IsNullOrWhiteSpace(message))
    //        return Array.Empty<string>();

    //    // Preserve empty fields: ShowBoxPage expects the
    //    // original protocol field positions to remain intact.
    //    string[] parts = message
    //        .Split('◆', StringSplitOptions.None)
    //        .Select(part => part.Trim())
    //        .ToArray();

    //    // Remove the terminator only from the final field.
    //    if (parts.Length > 0)
    //    {
    //        parts[^1] = parts[^1]
    //            .Replace("◉", "")
    //            .Trim();
    //    }

    //    // Remove only trailing empty fields, not empty fields
    //    // between protocol delimiters.
    //    int length = parts.Length;

    //    while (length > 0 &&
    //           string.IsNullOrWhiteSpace(parts[length - 1]))
    //    {
    //        length--;
    //    }

    //    return parts.Take(length).ToArray();
    //}



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

    //public async Task SendHexAsync(
    //    string hex)
    //{
    //    if (string.IsNullOrWhiteSpace(hex))
    //        return;

    //    string cleaned =
    //        hex
    //            .Replace(" ", "")
    //            .Replace("-", "")
    //            .Replace("\r", "")
    //            .Replace("\n", "");

    //    if (string.IsNullOrWhiteSpace(cleaned))
    //        return;

    //    byte[] data;

    //    try
    //    {
    //        data =
    //            Convert.FromHexString(
    //                cleaned);
    //    }
    //    catch (FormatException)
    //    {
    //        Console.WriteLine(
    //            "Invalid HEX data.");

    //        return;
    //    }

    //    await _tcpServer.SendAsync(data);
    //}
    public async Task SendHexAsync(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return;

        string cleaned = hex
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
            Console.WriteLine("Invalid HEX data.");
            return;
        }

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

        await _tcpServer.SendAsync(connectionId, data);
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
        _tcpServer.ClientDisconnected -= OnTcpClientDisconnected;
        _discovery.DeviceIdentified -= OnDeviceIdentified;

        Stop();
    }
}