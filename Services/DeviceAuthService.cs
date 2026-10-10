using MyBlazorApp1.Models;

namespace MyBlazorApp1.Services;

public class DeviceAuthService
{
    private readonly DiagDeviceService _deviceService;
    private readonly OtpService _otpService;

    public DeviceAuthService(
        DiagDeviceService deviceService,
        OtpService otpService)
    {
        _deviceService = deviceService;
        _otpService = otpService;
    }

    public async Task<bool> ValidateDeviceAsync(
        string serialNumber,
        string pairingCode)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return false;

        if (string.IsNullOrWhiteSpace(pairingCode))
            return false;

        var device = await _deviceService
            .GetBySerialNumberAsync(serialNumber);

        if (device == null)
            return false;

        if (device.IsBlocked)
            return false;

        return await _deviceService
            .VerifyPairingCodeAsync(
                serialNumber,
                pairingCode);
    }

    public async Task<string?> CreateLoginOtpAsync(
        string serialNumber,
        string pairingCode)
    {
        bool valid = await ValidateDeviceAsync(
            serialNumber,
            pairingCode);

        if (!valid)
            return null;

        return await _otpService.CreateOtpAsync(
            serialNumber,
            "CustomerLogin");
    }

    public async Task<string?> CompleteFirstLoginAsync(
        string serialNumber,
        string pairingCode,
        string otpCode,
        string installationId)
    {
        if (string.IsNullOrWhiteSpace(installationId))
            return null;

        bool valid = await ValidateDeviceAsync(
            serialNumber,
            pairingCode);

        if (!valid)
            return null;

        bool otpValid = await _otpService.VerifyOtpAsync(
            serialNumber,
            "CustomerLogin",
            otpCode);

        if (!otpValid)
            return null;

        string deviceToken =
            _deviceService.GenerateDeviceToken();

        bool tokenSaved =
            await _deviceService.SetDeviceTokenAsync(
                serialNumber,
                deviceToken);

        if (!tokenSaved)
            return null;

        bool installationSaved =
            await _deviceService.SetInstallationIdAsync(
                serialNumber,
                installationId);

        if (!installationSaved)
            return null;

        return deviceToken;
    }

    public async Task<bool> ValidateDeviceTokenAsync(
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

        return await _deviceService
            .VerifyDeviceTokenAsync(
                serialNumber,
                installationId,
                deviceToken);
    }

    public async Task<bool> AutoLoginAsync(
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

        return await _deviceService.VerifyDeviceTokenAsync(
            serialNumber,
            installationId,
            deviceToken);
    }


    public async Task<bool> LogoutAsync(
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

        // First make sure this is the currently
        // registered installation and token.
        bool valid = await _deviceService.VerifyDeviceTokenAsync(
            serialNumber,
            installationId,
            deviceToken);

        if (!valid)
            return false;

        return await _deviceService
            .InvalidateAuthenticationAsync(serialNumber);
    }
}