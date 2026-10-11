
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyBlazorApp1.Services;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;
using MyBlazorApp1.Models;

namespace MyBlazorApp1.Api;

public static class OperatorEndpoints
{
    public static void MapOperatorEndpoints(this WebApplication app)
    {
        app.MapPost("/api/operator/login",
            async (
                OperatorLoginRequest request,
                OperatorAuthService authService,
                HttpContext http) =>
            {
                if (string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Username and password are required."
                    });
                }

                bool valid = await authService.ValidateCredentialsAsync(
                    request.Username.Trim(),
                    request.Password);

                if (!valid)
                {
                    return Results.Json(
                        new
                        {
                            success = false,
                            message = "Invalid username or password."
                        },
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                var claims = new[]
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        request.Username.Trim()),
                    new Claim(
                        ClaimTypes.Name,
                        request.Username.Trim()),
                    new Claim(
                        ClaimTypes.Role,
                        "Operator")
                };

                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var principal = new ClaimsPrincipal(identity);

                await http.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = false,
                        AllowRefresh = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                    });

                return Results.Ok(new
                {
                    success = true,
                    message = "Operator login successful."
                });
            });

        app.MapPost("/api/operator/logout",
            async (HttpContext http) =>
            {
                await http.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return Results.Ok(new
                {
                    success = true,
                    message = "Operator logout successful."
                });
            })
            .RequireAuthorization();

        app.MapGet("/api/operator/me", (ClaimsPrincipal user) =>
        {
            return Results.Ok(new
            {
                authenticated = true,
                username = user.Identity?.Name,
                role = "Operator"
            });
        })
        .RequireAuthorization();

        app.MapPost("/api/operator/remote-diagnostic/request-otp",
            async (
                RemoteOtpRequest request,
                OtpService otpService,
                IDbContextFactory<DiagDbContext> dbFactory,
                IWebHostEnvironment environment) =>
            {
                if (string.IsNullOrWhiteSpace(request.SerialNumber))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Serial number is required."
                    });
                }

                // SMS is not integrated yet.
                // Never generate an OTP through this endpoint in Production
                // until a real SMS delivery service is connected.
                if (!environment.IsDevelopment())
                {
                    return Results.Problem(
                        title: "SMS service is not configured.",
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }

                await using var db = await dbFactory.CreateDbContextAsync();

                var serialNumber = request.SerialNumber.Trim();

                var device = await db.DiagInfos
                    .SingleOrDefaultAsync(x => x.SerialNumber == serialNumber);

                if (device == null || device.IsBlocked)
                {
                    return Results.NotFound(new
                    {
                        success = false,
                        message = "Device not found or blocked."
                    });
                }

                if (string.IsNullOrWhiteSpace(device.OwnerPhoneNumber))
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "The device has no registered owner phone number."
                    });
                }

                var code = await otpService.CreateOtpAsync(
                    serialNumber,
                    "RemoteDiagnostic",
                    validityMinutes: 5);

                if (code == null)
                {
                    return Results.Problem(
                        title: "Could not create OTP.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                var phone = device.OwnerPhoneNumber;
                var maskedPhone = phone.Length <= 4
                    ? "****"
                    : new string('*', phone.Length - 4) + phone[^4..];

                // Development-only test response.
                // This must not be used as the production SMS flow.
                return Results.Ok(new
                {
                    success = true,
                    message = "Development OTP created. No SMS was sent.",
                    destination = maskedPhone,
                    expiresInMinutes = 5,
                    developmentOtp = code
                });
            })
            .RequireAuthorization(policy => policy.RequireRole("Operator"));

        app.MapPost("/api/operator/remote-diagnostic/verify-otp",
            async (
                RemoteVerifyOtpRequest request,
                ClaimsPrincipal user,
                OtpService otpService,
                IDbContextFactory<DiagDbContext> dbFactory) =>
            {
                if (string.IsNullOrWhiteSpace(request.SerialNumber) ||
                    string.IsNullOrWhiteSpace(request.Code))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Serial number and OTP code are required."
                    });
                }

                var serialNumber = request.SerialNumber.Trim();
                var username = user.Identity?.Name;

                if (string.IsNullOrWhiteSpace(username))
                {
                    return Results.Unauthorized();
                }

                // Resolve the authenticated operator from the database.
                await using var db = await dbFactory.CreateDbContextAsync();

                var operatorAccount = await db.OperatorAccounts
                    .SingleOrDefaultAsync(x =>
                        x.Username == username && x.IsActive);

                if (operatorAccount == null)
                {
                    return Results.Unauthorized();
                }

                var device = await db.DiagInfos
                    .SingleOrDefaultAsync(x => x.SerialNumber == serialNumber);

                if (device == null || device.IsBlocked)
                {
                    return Results.NotFound(new
                    {
                        success = false,
                        message = "Device not found or blocked."
                    });
                }

                var valid = await otpService.VerifyOtpAsync(
                    serialNumber,
                    "RemoteDiagnostic",
                    request.Code.Trim());

                if (!valid)
                {
                    return Results.Json(
                        new
                        {
                            success = false,
                            message = "Invalid or expired OTP."
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // Generate a cryptographically random session token.
                var tokenBytes = RandomNumberGenerator.GetBytes(32);
                var sessionToken = Convert.ToBase64String(tokenBytes);

                var session = new RemoteDiagnosticSession
                {
                    OperatorAccountId = operatorAccount.Id,
                    DiagInfoId = device.Id,
                    SessionTokenHash = Convert.ToHexString(
                        SHA256.HashData(tokenBytes)),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                    RevokedAt = null
                };

                db.RemoteDiagnosticSessions.Add(session);
                await db.SaveChangesAsync();

                // The raw token is returned once; only its hash is stored.
                return Results.Ok(new
                {
                    success = true,
                    message = "Remote diagnostic session created.",
                    sessionToken,
                    serialNumber,
                    expiresAtUtc = session.ExpiresAt
                });
            })
            .RequireAuthorization(policy => policy.RequireRole("Operator"));


    }
    public sealed record RemoteVerifyOtpRequest(
    string SerialNumber,
    string Code);
    public sealed record RemoteOtpRequest(string SerialNumber);
    public sealed record OperatorLoginRequest(
        string Username,
        string Password);
}
