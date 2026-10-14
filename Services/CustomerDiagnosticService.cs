using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;

namespace MyBlazorApp1.Services;

public sealed record CustomerDiagnosticDto(
    string SerialNumber,
    string SoftwareVersion,
    string Counter,
    bool IsOnline);

public sealed class CustomerDiagnosticService
{
    private readonly IDbContextFactory<DiagDbContext> _dbFactory;
    private readonly OnlineDiagnosticDiscoveryService _discovery;

    public CustomerDiagnosticService(
        IDbContextFactory<DiagDbContext> dbFactory,
        OnlineDiagnosticDiscoveryService discovery)
    {
        _dbFactory = dbFactory;
        _discovery = discovery;
    }

    public async Task<IReadOnlyList<CustomerDiagnosticDto>> GetForPhoneAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            await _dbFactory.CreateDbContextAsync(cancellationToken);

        // فقط دیاگ‌های ثبت‌شده برای همین مشتری
        var ownedDevices = await db.DiagInfos
            .AsNoTracking()
            .Where(x =>
                x.OwnerPhoneNumber == phoneNumber &&
                !x.IsBlocked)
            .Select(x => x.SerialNumber)
            .Distinct()
            .ToListAsync(cancellationToken);

        var onlineBySerial = _discovery.GetOnlineDevices()
            .GroupBy(x => x.SerialNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Counter).First(),
                StringComparer.OrdinalIgnoreCase);

        return ownedDevices
            .Select(serial =>
            {
                var isOnline = onlineBySerial.TryGetValue(
                    serial, out var identity);

                return new CustomerDiagnosticDto(
                    serial,
                    isOnline ? identity!.SoftwareVersion : "",
                    isOnline ? identity!.Counter : "",
                    isOnline);
            })
            .OrderByDescending(x => x.IsOnline)
            .ThenBy(x => x.SerialNumber)
            .ToArray();
    }
}