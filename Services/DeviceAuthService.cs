
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

    public async Task<string?> CreateLoginOtpAsync(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return null;

        var device = await _deviceService.GetBySerialNumberAsync(serialNumber);

        if (device == null || device.IsBlocked ||
            string.IsNullOrWhiteSpace(device.OwnerPhoneNumber))
        {
            return null;
        }

        return await _otpService.CreateOtpAsync(
            serialNumber, "CustomerLogin");
    }

    public async Task<string?> CompleteFirstLoginAsync(
        string serialNumber,
        string otpCode,
        string installationId)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) ||
            string.IsNullOrWhiteSpace(otpCode) ||
            string.IsNullOrWhiteSpace(installationId))
        {
            return null;
        }

        var device = await _deviceService.GetBySerialNumberAsync(serialNumber);

        if (device == null || device.IsBlocked ||
            string.IsNullOrWhiteSpace(device.OwnerPhoneNumber))
        {
            return null;
        }

        bool otpValid = await _otpService.VerifyOtpAsync(
            serialNumber, "CustomerLogin", otpCode);

        if (!otpValid)
            return null;

        string deviceToken = _deviceService.GenerateDeviceToken();

        bool tokenSaved = await _deviceService.SetDeviceTokenAsync(
            serialNumber, deviceToken);

        if (!tokenSaved)
            return null;

        bool installationSaved = await _deviceService.SetInstallationIdAsync(
            serialNumber, installationId);

        if (!installationSaved)
        {
            await _deviceService.RevokeDeviceTokenAsync(serialNumber);
            return null;
        }

        return deviceToken;
    }

    public async Task<bool> ValidateDeviceTokenAsync(
        string serialNumber,
        string installationId,
        string deviceToken)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) ||
            string.IsNullOrWhiteSpace(installationId) ||
            string.IsNullOrWhiteSpace(deviceToken))
        {
            return false;
        }

        return await _deviceService.VerifyDeviceTokenAsync(
            serialNumber, installationId, deviceToken);
    }

    public Task<bool> AutoLoginAsync(
        string serialNumber,
        string installationId,
        string deviceToken) =>
        ValidateDeviceTokenAsync(
            serialNumber, installationId, deviceToken);

    public async Task<bool> LogoutAsync(
        string serialNumber,
        string installationId,
        string deviceToken)
    {
        bool valid = await ValidateDeviceTokenAsync(
            serialNumber, installationId, deviceToken);

        if (!valid)
            return false;

        return await _deviceService.InvalidateAuthenticationAsync(
            serialNumber);
    }
}
