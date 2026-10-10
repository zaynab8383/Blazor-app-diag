using Microsoft.AspNetCore.Http;
using MyBlazorApp1.Services;

namespace MyBlazorApp1.Api;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // ================================
        // Request OTP
        // ================================
        app.MapPost("/api/auth/request-otp",
            async (
                RequestOtpRequest request,
                DeviceAuthService authService) =>
            {
                if (string.IsNullOrWhiteSpace(request.SerialNumber) ||
                    string.IsNullOrWhiteSpace(request.PairingCode) ||
                    string.IsNullOrWhiteSpace(request.InstallationId))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "SerialNumber, PairingCode and InstallationId are required."
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
                // Later this OTP will be sent by SMS.
                return Results.Ok(new
                {
                    success = true,
                    message = "OTP created.",
                    otp = otp
                });
            });


        // ================================
        // Complete First Login
        // ================================
        app.MapPost("/api/auth/login",
            async (
                LoginRequest request,
                DeviceAuthService authService) =>
            {
                if (string.IsNullOrWhiteSpace(request.SerialNumber) ||
                    string.IsNullOrWhiteSpace(request.PairingCode) ||
                    string.IsNullOrWhiteSpace(request.OtpCode) ||
                    string.IsNullOrWhiteSpace(request.InstallationId))
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
                        request.OtpCode,
                        request.InstallationId);

                if (deviceToken == null)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new
                {
                    success = true,
                    serialNumber = request.SerialNumber,
                    installationId = request.InstallationId,
                    deviceToken = deviceToken
                });
            });

        // ================================
        // Auto Login
        // ================================
        app.MapPost("/api/auth/auto-login",
            async (
                AutoLoginRequest request,
                DeviceAuthService authService) =>
            {
                if (string.IsNullOrWhiteSpace(request.SerialNumber) ||
                    string.IsNullOrWhiteSpace(request.InstallationId) ||
                    string.IsNullOrWhiteSpace(request.DeviceToken))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "SerialNumber, InstallationId and DeviceToken are required."
                    });
                }

                bool valid =
                    await authService.AutoLoginAsync(
                        request.SerialNumber,
                        request.InstallationId,
                        request.DeviceToken);

                if (!valid)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new
                {
                    success = true,
                    serialNumber = request.SerialNumber,
                    installationId = request.InstallationId,
                    message = "Auto login successful."
                });
            });

        // ================================
        // Logout
        // ================================
        app.MapPost("/api/auth/logout",
            async (
                LogoutRequest request,
                DeviceAuthService authService) =>
            {
                if (string.IsNullOrWhiteSpace(request.SerialNumber) ||
                    string.IsNullOrWhiteSpace(request.InstallationId) ||
                    string.IsNullOrWhiteSpace(request.DeviceToken))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "SerialNumber, InstallationId and DeviceToken are required."
                    });
                }

                bool success =
                    await authService.LogoutAsync(
                        request.SerialNumber,
                        request.InstallationId,
                        request.DeviceToken);

                if (!success)
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new
                {
                    success = true,
                    message = "Logout successful."
                });
            });
    }


    // ================================
    // Request OTP model
    // ================================
    public record RequestOtpRequest(
        string SerialNumber,
        string PairingCode,
        string InstallationId);


    // ================================
    // Login model
    // ================================
    public record LoginRequest(
        string SerialNumber,
        string PairingCode,
        string OtpCode,
        string InstallationId);
    public record AutoLoginRequest(
    string SerialNumber,
    string InstallationId,
    string DeviceToken);


    public record LogoutRequest(
    string SerialNumber,
    string InstallationId,
    string DeviceToken);
}

