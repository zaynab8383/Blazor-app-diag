using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;
using MyBlazorApp1.Models;


namespace MyBlazorApp1.Services;

public class DiagDeviceService
{
    private readonly IDbContextFactory<DiagDbContext> _dbFactory;

    public DiagDeviceService(IDbContextFactory<DiagDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // =========================================================
    // GET DEVICE
    // =========================================================

    public async Task<DiagInfo?> GetBySerialNumberAsync(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return null;

        serialNumber = serialNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);
    }

    public async Task<bool> ExistsAsync(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return false;

        serialNumber = serialNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.DiagInfos
            .AnyAsync(x => x.SerialNumber == serialNumber);
    }

    public async Task<bool> IsBlockedAsync(string serialNumber)
    {
        var device = await GetBySerialNumberAsync(serialNumber);

        return device?.IsBlocked ?? false;
    }

    // =========================================================
    // INSTALLATION ID
    // =========================================================

    public async Task<bool> SetInstallationIdAsync(
        string serialNumber,
        string installationId)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) ||
            string.IsNullOrWhiteSpace(installationId))
        {
            return false;
        }

        serialNumber = serialNumber.Trim();
        installationId = installationId.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        if (device == null)
            return false;

        device.InstallationId = installationId;
        device.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<string?> GetInstallationIdAsync(
        string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return null;

        serialNumber = serialNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        return device?.InstallationId;
    }

    // =========================================================
    // DEVICE TOKEN
    // =========================================================

    public string GenerateDeviceToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public string HashSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException(
                "Secret cannot be empty.",
                nameof(secret));
        }

        using SHA256 sha256 = SHA256.Create();

        byte[] bytes = Encoding.UTF8.GetBytes(secret);
        byte[] hash = sha256.ComputeHash(bytes);

        return Convert.ToHexString(hash);
    }

    public async Task<bool> SetDeviceTokenAsync(
        string serialNumber,
        string deviceToken)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) ||
            string.IsNullOrWhiteSpace(deviceToken))
        {
            return false;
        }

        serialNumber = serialNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        if (device == null)
            return false;

        device.DeviceTokenHash = HashSecret(deviceToken);
        device.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> VerifyDeviceTokenAsync(
    string serialNumber,
    string installationId,
    string deviceToken)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return false;

        if (string.IsNullOrWhiteSpace(installationId))
            return false;

        if (string.IsNullOrWhiteSpace(deviceToken))
            return false;

        await using var db =
            await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x =>
                x.SerialNumber == serialNumber);

        if (device == null)
            return false;

        if (device.IsBlocked)
            return false;

        // InstallationId must belong to this installation.
        if (!string.Equals(
                device.InstallationId,
                installationId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(device.DeviceTokenHash))
            return false;

        string tokenHash = HashSecret(deviceToken);

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(device.DeviceTokenHash),
            Convert.FromHexString(tokenHash));
    }

    // =========================================================
    // PAIRING CODE
    // =========================================================

    //public async Task<bool> SetPairingCodeAsync(
    //    string serialNumber,
    //    string pairingCode)
    //{
    //    if (string.IsNullOrWhiteSpace(serialNumber) ||
    //        string.IsNullOrWhiteSpace(pairingCode))
    //    {
    //        return false;
    //    }

    //    serialNumber = serialNumber.Trim();

    //    await using var db = await _dbFactory.CreateDbContextAsync();

    //    var device = await db.DiagInfos
    //        .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

    //    if (device == null)
    //        return false;

    //    //device.PairingCodeHash = HashSecret(pairingCode);
    //    device.UpdatedAt = DateTime.UtcNow;

    //    await db.SaveChangesAsync();

    //    return true;
    //}

    //public async Task<bool> VerifyPairingCodeAsync(
    //    string serialNumber,
    //    string pairingCode)
    //{
    //    if (string.IsNullOrWhiteSpace(serialNumber) ||
    //        string.IsNullOrWhiteSpace(pairingCode))
    //    {
    //        return false;
    //    }

    //    serialNumber = serialNumber.Trim();

    //    await using var db = await _dbFactory.CreateDbContextAsync();

    //    var device = await db.DiagInfos
    //        .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

    //    if (device == null)
    //        return false;

    //    if (device.IsBlocked)
    //        return false;

    //    if (string.IsNullOrWhiteSpace(device.PairingCodeHash))
    //        return false;

    //    string hash = HashSecret(pairingCode);

    //    return CryptographicOperations.FixedTimeEquals(
    //        Convert.FromHexString(device.PairingCodeHash),
    //        Convert.FromHexString(hash));
    //}

    // =========================================================
    // AUTHENTICATION / TOKEN REVOCATION
    // =========================================================

    public async Task<bool> RevokeDeviceTokenAsync(
        string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return false;

        serialNumber = serialNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        if (device == null)
            return false;

        device.DeviceTokenHash = null;
        device.AuthVersion++;
        device.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> InvalidateAuthenticationAsync(
        string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return false;

        serialNumber = serialNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        if (device == null)
            return false;

        device.DeviceTokenHash = null;
        device.AuthVersion++;
        device.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return true;
    }

    // =========================================================
    // BLOCK / UNBLOCK
    // =========================================================

    public async Task<bool> BlockAsync(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return false;

        serialNumber = serialNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        if (device == null)
            return false;

        device.IsBlocked = true;
        device.DeviceTokenHash = null;
        device.AuthVersion++;
        device.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UnblockAsync(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return false;

        serialNumber = serialNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        if (device == null)
            return false;

        device.IsBlocked = false;
        device.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return true;
    }

    // =========================================================
    // CHANGE OWNER PHONE
    // =========================================================

    public async Task<bool> ChangeOwnerPhoneAsync(
        string serialNumber,
        string newOwnerPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) ||
            string.IsNullOrWhiteSpace(newOwnerPhoneNumber))
        {
            return false;
        }

        serialNumber = serialNumber.Trim();
        newOwnerPhoneNumber = newOwnerPhoneNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        if (device == null)
            return false;

        device.OwnerPhoneNumber = newOwnerPhoneNumber;

        // ورود قبلی باید باطل شود
        device.DeviceTokenHash = null;
        device.InstallationId = null;
        device.AuthVersion++;

        device.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return true;
    }

    // =========================================================
    // CHANGE PAIRING CODE
    // =========================================================

    public async Task<bool> ChangePairingCodeAsync(
        string serialNumber,
        string newPairingCode)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) ||
            string.IsNullOrWhiteSpace(newPairingCode))
        {
            return false;
        }

        serialNumber = serialNumber.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var device = await db.DiagInfos
            .FirstOrDefaultAsync(x => x.SerialNumber == serialNumber);

        if (device == null)
            return false;

        //device.PairingCodeHash = HashSecret(newPairingCode);

        // Token قبلی دیگر معتبر نباشد
        device.DeviceTokenHash = null;
        device.InstallationId = null;
        device.AuthVersion++;

        device.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return true;
    }
}