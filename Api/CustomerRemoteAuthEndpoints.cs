
using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;
using MyBlazorApp1.Services;

namespace MyBlazorApp1.Api;

public static class CustomerRemoteAuthEndpoints
{
    public static void MapCustomerRemoteAuthEndpoints(
        this WebApplication app)
    {
        // مرحله اول: مشتری فقط شماره موبایل را وارد می‌کند.
        app.MapPost(
            "/api/customer/remote-diagnostic/request-otp",
            async (
                CustomerRequestOtpRequest request,
                OtpService otpService,
                IDbContextFactory<DiagDbContext> dbFactory,
                IWebHostEnvironment environment) =>
            {
                if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Phone number is required."
                    });
                }

                string phoneNumber = request.PhoneNumber.Trim();

                // تا قبل از اتصال سرویس پیامک واقعی،
                // OTP فقط در محیط Development مجاز است.
                if (!environment.IsDevelopment())
                {
                    return Results.Problem(
                        title: "SMS service is not configured.",
                        statusCode:
                            StatusCodes.Status503ServiceUnavailable);
                }

                await using var db =
                    await dbFactory.CreateDbContextAsync();

                bool hasEligibleDevice = await db.DiagInfos
                    .AnyAsync(x =>
                        x.OwnerPhoneNumber == phoneNumber &&
                        !x.IsBlocked);

                // از افشای ثبت بودن یا نبودن شماره جلوگیری می‌کنیم.
                if (!hasEligibleDevice)
                {
                    return Results.Ok(new
                    {
                        success = true,
                        message =
                            "If the number is registered, an OTP will be sent.",
                        expiresInMinutes = 5
                    });
                }

                string? code =
                    await otpService.CreateCustomerRemoteOtpAsync(
                        phoneNumber,
                        validityMinutes: 5);

                if (code == null)
                {
                    return Results.Problem(
                        title: "Could not create OTP.",
                        statusCode:
                            StatusCodes.Status500InternalServerError);
                }

                string maskedPhone = phoneNumber.Length <= 4
                    ? "****"
                    : new string('*', phoneNumber.Length - 4)
                        + phoneNumber[^4..];

                // فقط برای تست؛ هنوز پیامکی ارسال نمی‌شود.
                return Results.Ok(new
                {
                    success = true,
                    message =
                        "Development OTP created. No SMS was sent.",
                    destination = maskedPhone,
                    expiresInMinutes = 5,
                    developmentOtp = code
                });
            });

        // مرحله دوم: تأیید OTP و دریافت دیاگ‌های مجاز.
        app.MapPost(
            "/api/customer/remote-diagnostic/verify-otp",
            async (
                CustomerVerifyOtpRequest request,
                OtpService otpService,
                IDbContextFactory<DiagDbContext> dbFactory) =>
            {
                if (string.IsNullOrWhiteSpace(request.PhoneNumber) ||
                    string.IsNullOrWhiteSpace(request.Code))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message =
                            "Phone number and OTP code are required."
                    });
                }

                string phoneNumber = request.PhoneNumber.Trim();

                bool valid =
                    await otpService.VerifyCustomerRemoteOtpAsync(
                        phoneNumber,
                        request.Code);

                if (!valid)
                {
                    return Results.Json(
                        new
                        {
                            success = false,
                            message = "Invalid or expired OTP."
                        },
                        statusCode:
                            StatusCodes.Status400BadRequest);
                }

                await using var db =
                    await dbFactory.CreateDbContextAsync();

                var devices = await db.DiagInfos
                    .Where(x =>
                        x.OwnerPhoneNumber == phoneNumber &&
                        !x.IsBlocked)
                    .OrderBy(x => x.SerialNumber)
                    .Select(x => new
                    {
                        id = x.Id,
                        serialNumber = x.SerialNumber
                    })
                    .ToListAsync();

                if (devices.Count == 0)
                {
                    return Results.Json(
                        new
                        {
                            success = false,
                            message =
                                "No available diagnostic devices are registered to this number."
                        },
                        statusCode:
                            StatusCodes.Status403Forbidden);
                }

                return Results.Ok(new
                {
                    success = true,
                    requiresDeviceSelection = devices.Count > 1,
                    devices
                });
            });
    }

    public sealed record CustomerRequestOtpRequest(
        string PhoneNumber);

    public sealed record CustomerVerifyOtpRequest(
        string PhoneNumber,
        string Code);
}
