using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;
using MyBlazorApp1.Models;

namespace MyBlazorApp1.Services;

public class OtpService
{
    private readonly IDbContextFactory<DiagDbContext> _dbFactory;
    private readonly DiagDeviceService _deviceService;

    public OtpService(
        IDbContextFactory<DiagDbContext> dbFactory,
        DiagDeviceService deviceService)
    {
        _dbFactory = dbFactory;
        _deviceService = deviceService;
    }

    public async Task<string?> CreateOtpAsync(
        string serialNumber,
        string purpose,
        int validityMinutes = 5)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return null;

        if (string.IsNullOrWhiteSpace(purpose))
            return null;

        var device = await _deviceService
            .GetBySerialNumberAsync(serialNumber);

        if (device == null || device.IsBlocked)
            return null;

        // Generate a 6-digit OTP
        string code = RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();
       

        await using var db = await _dbFactory.CreateDbContextAsync();

        // Invalidate previous unused OTPs for the same device/purpose
        var oldOtps = await db.Otps
            .Where(x =>
                x.DiagInfoId == device.Id &&
                x.Purpose == purpose &&
                x.UsedAt == null &&
                x.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var oldOtp in oldOtps)
        {
            oldOtp.UsedAt = DateTime.UtcNow;
        }

        string codeHash = _deviceService.HashSecret(code);

        var otp = new Otp
        {
            DiagInfoId = device.Id,
            CodeHash = codeHash,
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(validityMinutes),
            CreatedAt = DateTime.UtcNow,
            UsedAt = null
        };

        db.Otps.Add(otp);

        await db.SaveChangesAsync();

        // TEST ONLY:
        // Later this code will be sent by SMS instead.
        return code;
    }

    public async Task<bool> VerifyOtpAsync(
        string serialNumber,
        string purpose,
        string code)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) ||
            string.IsNullOrWhiteSpace(purpose) ||
            string.IsNullOrWhiteSpace(code))
            return false;

        var device = await _deviceService
            .GetBySerialNumberAsync(serialNumber);

        if (device == null || device.IsBlocked)
            return false;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var otp = await db.Otps
            .Where(x =>
                x.DiagInfoId == device.Id &&
                x.Purpose == purpose &&
                x.UsedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        if (otp == null)
            return false;

        if (otp.ExpiresAt <= DateTime.UtcNow)
            return false;
        Console.WriteLine(
            $"OTP DEBUG: serial={serialNumber}, purpose={purpose}, codeLength={code.Length}");

        string codeHash = _deviceService.HashSecret(code);

        Console.WriteLine($"OTP DEBUG: inputHash={codeHash}");
        Console.WriteLine($"OTP DEBUG: storedHash={otp.CodeHash}");
        //string codeHash = _deviceService.HashSecret(code);

        bool valid = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(otp.CodeHash),
            Convert.FromHexString(codeHash));

        if (!valid)
            return false;

        // OTP is single-use
        otp.UsedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return true;
    }


    public async Task<string?> CreateCustomerRemoteOtpAsync(
        string phoneNumber,
        int validityMinutes = 5)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        phoneNumber = phoneNumber.Trim();

        var now = DateTime.UtcNow;
        const string purpose = "CustomerRemoteLogin";

        await using var db =
            await _dbFactory.CreateDbContextAsync();


        var devices = await db.DiagInfos
            .Where(x =>
                x.OwnerPhoneNumber == phoneNumber &&
                !x.IsBlocked)
            .Select(x => new { x.Id })
            .ToListAsync();

        if (devices.Count == 0)
            return null;

        string code = RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();

        string codeHash = _deviceService.HashSecret(code);

        var deviceIds = devices.Select(x => x.Id).ToList();

        var previousOtps = await db.Otps
            .Where(x =>
                deviceIds.Contains(x.DiagInfoId) &&
                x.Purpose == purpose &&
                x.UsedAt == null)
            .ToListAsync();

        foreach (var previousOtp in previousOtps)
            previousOtp.UsedAt = now;

        foreach (var device in devices)
        {
            db.Otps.Add(new Otp
            {
                DiagInfoId = device.Id,
                CodeHash = codeHash,
                Purpose = purpose,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(validityMinutes),
                UsedAt = null
            });
        }

        await db.SaveChangesAsync();

        return code;
    }

    public async Task<bool> VerifyCustomerRemoteOtpAsync(
        string phoneNumber,
        string code)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) ||
            string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        phoneNumber = phoneNumber.Trim();
        code = code.Trim();

        var now = DateTime.UtcNow;
        const string purpose = "CustomerRemoteLogin";

        await using var db =
            await _dbFactory.CreateDbContextAsync();

        var deviceIds = await db.DiagInfos
            .Where(x =>
                x.OwnerPhoneNumber == phoneNumber &&
                !x.IsBlocked)
            .Select(x => x.Id)
            .ToListAsync();

        if (deviceIds.Count == 0)
            return false;

        var candidateOtps = await db.Otps
            .Where(x =>
                deviceIds.Contains(x.DiagInfoId) &&
                x.Purpose == purpose &&
                x.UsedAt == null &&
                x.ExpiresAt > now)
            .ToListAsync();

        string inputHash = _deviceService.HashSecret(code);

        var matchingOtp = candidateOtps.FirstOrDefault(x =>
        {
            try
            {
                return CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(x.CodeHash),
                    Convert.FromHexString(inputHash));
            }
            catch (FormatException)
            {
                return false;
            }
        });

        if (matchingOtp == null)
            return false;


        string matchingHash = matchingOtp.CodeHash;

        var allCopies = await db.Otps
            .Where(x =>
                deviceIds.Contains(x.DiagInfoId) &&
                x.Purpose == purpose &&
                x.UsedAt == null &&
                x.CodeHash == matchingHash)
            .ToListAsync();

        foreach (var otp in allCopies)
            otp.UsedAt = now;

        await db.SaveChangesAsync();

        return true;
    }

}