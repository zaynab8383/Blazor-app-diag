using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;
using MyBlazorApp1.Models;

namespace MyBlazorApp1.Services;

public class DiagDeviceService
{
    private readonly DiagDbContext _db;

    public DiagDeviceService(DiagDbContext db)
    {
        _db = db;
    }

    public async Task<DiagInfo?> GetBySerialNumberAsync(string serialNumber)
    {
        return await _db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);
    }

    public async Task<bool> ExistsAsync(string serialNumber)
    {
        return await _db.DiagInfos
            .AnyAsync(x => x.SerialNumber == serialNumber);
    }

    public async Task<bool> IsBlockedAsync(string serialNumber)
    {
        var device = await GetBySerialNumberAsync(serialNumber);

        return device?.IsBlocked ?? false;
    }
}