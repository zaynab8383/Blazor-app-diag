using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Api;
using MyBlazorApp1.Components;
using MyBlazorApp1.Data;
using MyBlazorApp1.Services;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// SERVICES
// =========================================================

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<UdpServer>();
builder.Services.AddSingleton<DiagnosticTcpServerService>();
builder.Services.AddSingleton<DiagnosticService>();
builder.Services.AddSingleton<DiagnosticMenuService>();
builder.Services.AddSingleton<SerialService>();

builder.Services.AddScoped<DiagDeviceService>();
builder.Services.AddScoped<DeviceAuthService>();
builder.Services.AddScoped<OtpService>();

// =========================================================
// DATABASE
// =========================================================

builder.Services.AddDbContextFactory<DiagDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DiagConnection")));

// =========================================================
// BUILD APP
// =========================================================

var app = builder.Build();

// =========================================================
// HTTP REQUEST PIPELINE
// =========================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// =========================================================
// BLAZOR
// =========================================================

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// =========================================================
// AUTHENTICATION ENDPOINTS
// =========================================================

// Existing OTP and login endpoints
app.MapAuthEndpoints();

// Validate a saved device token
app.MapPost("/api/auth/validate-token",
    async (
        TokenValidationRequest request,
        DeviceAuthService authService) =>
    {
        if (string.IsNullOrWhiteSpace(request.SerialNumber) ||
            string.IsNullOrWhiteSpace(request.InstallationId) ||
            string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            return Results.BadRequest(new
            {
                success = false,
                message = "All fields are required."
            });
        }

        bool valid = await authService.AutoLoginAsync(
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
            message = "Token is valid."
        });
    });

// =========================================================
// START APP
// =========================================================

app.Run();

// Request model for token validation
public sealed record TokenValidationRequest(
    string SerialNumber,
    string InstallationId,
    string DeviceToken);
