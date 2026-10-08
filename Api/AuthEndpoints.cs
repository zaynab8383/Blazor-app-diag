using Microsoft.AspNetCore.Http;
using MyBlazorApp1.Services;

namespace MyBlazorApp1.Api;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/request-otp",
            async (
                RequestOtpRequest request,
                DeviceAuthService authService) =>
            {
                if (string.IsNullOrWhiteSpace(request.SerialNumber) ||
                    string.IsNullOrWhiteSpace(request.PairingCode))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "SerialNumber and PairingCode are required."
                    });
                }

                string? otp =
                    await authService.CreateLoginOtpAsync(
                        request.SerialNumber,
                        request.PairingCode);

                if (otp == null)
                {
                    return Results.Unauthorized();
                }

                // TEST ONLY
                // Later this will be sent by SMS.
                return Results.Ok(new
                {
                    success = true,
                    message = "OTP created.",
                    otp = otp
                });
            });

        app.MapPost("/api/auth/login",
            async (
                LoginRequest request,
                DeviceAuthService authService,
                DiagDeviceService deviceService) =>
            {
                if (string.IsNullOrWhiteSpace(request.SerialNumber) ||
                    string.IsNullOrWhiteSpace(request.PairingCode) ||
                    string.IsNullOrWhiteSpace(request.OtpCode))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "All fields are required."
                    });
                }

                string? deviceToken =
                    await authService.CompleteFirstLoginAsync(
                        request.SerialNumber,
                        request.PairingCode,
                        request.OtpCode);

                if (deviceToken == null)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new
                {
                    success = true,
                    serialNumber = request.SerialNumber,
                    deviceToken = deviceToken
                });
            });
    }

    public record RequestOtpRequest(
        string SerialNumber,
        string PairingCode);

    public record LoginRequest(
        string SerialNumber,
        string PairingCode,
        string OtpCode);
}