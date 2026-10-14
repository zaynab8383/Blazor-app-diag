namespace MyBlazorApp1.Models;

public class DiscoveredDiagnosticDevice
{
    public int Id { get; set; }

    public string SerialNumber { get; set; } = string.Empty;

    public string? SoftwareVersion { get; set; }

    // رشته نگه می‌داریم تا مقدار دریافتی دستگاه بدون تغییر حفظ شود.
    public string? Counter { get; set; }

    public DateTime FirstSeenAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }
}