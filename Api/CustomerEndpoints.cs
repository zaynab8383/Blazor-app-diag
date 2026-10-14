using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;
using MyBlazorApp1.Services;

namespace MyBlazorApp1.Api;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        app.MapPost("/api/customer/request-otp", async (
            RequestOtpRequest request,
            IDbContextFactory<DiagDbContext> dbFactory,
            OtpService otpService,
            IWebHostEnvironment environment) =>
        {
            var phone = request.PhoneNumber?.Trim();

            if (string.IsNullOrWhiteSpace(phone))
                return Results.BadRequest(new
                {
                    success = false,
                    message = "Phone number is required."
                });

            await using var db =
                await dbFactory.CreateDbContextAsync();

            var ownsDevice = await db.DiagInfos.AnyAsync(x =>
                x.OwnerPhoneNumber == phone && !x.IsBlocked);

            if (!ownsDevice)
                return Results.NotFound(new
                {
                    success = false,
                    message = "No purchased diagnostic is registered for this phone."
                });

            // پیامک واقعی هنوز باید به این نقطه متصل شود.
            if (!environment.IsDevelopment())
                return Results.Problem(
                    title: "SMS service is not configured.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            var code = await otpService.CreateCustomerRemoteOtpAsync(
                phone, validityMinutes: 5);

            if (code is null)
                return Results.Problem(
                    title: "Could not create OTP.",
                    statusCode: StatusCodes.Status500InternalServerError);

            return Results.Ok(new
            {
                success = true,
                message = "Development OTP created. No SMS was sent.",
                developmentOtp = code,
                expiresInMinutes = 5
            });
        });

        app.MapPost("/api/customer/verify-otp", async (
            VerifyOtpRequest request,
            HttpContext http,
            IDbContextFactory<DiagDbContext> dbFactory,
            OtpService otpService) =>
        {
            var phone = request.PhoneNumber?.Trim();
            var code = request.Code?.Trim();

            if (string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new
                {
                    success = false,
                    message = "Phone number and OTP are required."
                });

            await using var db =
                await dbFactory.CreateDbContextAsync();

            var ownsDevice = await db.DiagInfos.AnyAsync(x =>
                x.OwnerPhoneNumber == phone && !x.IsBlocked);

            if (!ownsDevice)
                return Results.Unauthorized();

            var valid = await otpService.VerifyCustomerRemoteOtpAsync(
                phone, code);

            if (!valid)
                return Results.Json(
                    new
                    {
                        success = false,
                        message = "Invalid or expired OTP."
                    },
                    statusCode: StatusCodes.Status401Unauthorized);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, phone),
                new Claim(ClaimTypes.MobilePhone, phone),
                new Claim(ClaimTypes.Role, "Customer")
            };

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await http.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
                });

            return Results.Ok(new
            {
                success = true,
                message = "Login successful."
            });
        });

        app.MapGet("/api/customer/diagnostics", async (
            ClaimsPrincipal user,
            CustomerDiagnosticService service,
            CancellationToken cancellationToken) =>
        {
            var phone = user.FindFirstValue(ClaimTypes.MobilePhone);

            if (string.IsNullOrWhiteSpace(phone))
                return Results.Unauthorized();

            var devices = await service.GetForPhoneAsync(
                phone, cancellationToken);

            return Results.Ok(devices);
        })
        .RequireAuthorization(policy =>
            policy.RequireRole("Customer"));

        app.MapPost("/api/customer/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return Results.Ok(new { success = true });
        })
        .RequireAuthorization(policy =>
            policy.RequireRole("Customer"));
    }

    public sealed record RequestOtpRequest(string PhoneNumber);
    public sealed record VerifyOtpRequest(string PhoneNumber, string Code);
}