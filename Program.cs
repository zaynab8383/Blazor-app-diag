using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Api;
using MyBlazorApp1.Components;
using MyBlazorApp1.Data;
using MyBlazorApp1.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

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
builder.Services.AddSingleton<OnlineDiagnosticDiscoveryService>();
builder.Services.AddHostedService<DiagnosticTcpServerHostedService>();

builder.Services.AddScoped<DiagDeviceService>();
builder.Services.AddScoped<DeviceAuthService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<OperatorAuthService>();

builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-MyBlazorOperator";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy =
            CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = false;

        // API requests should return status codes, not login redirects.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
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



// Ensure discovery subscribes to TCP events before clients connect.
_ = app.Services.GetRequiredService<OnlineDiagnosticDiscoveryService>();

var resetUsername = app.Configuration["OperatorPasswordReset:Username"];
var resetPassword = app.Configuration["OperatorPasswordReset:NewPassword"];



// =========================================================
// HTTP REQUEST PIPELINE
// =========================================================



var bootstrapUsername =
    app.Configuration["OperatorBootstrap:Username"];

var bootstrapPassword =
    app.Configuration["OperatorBootstrap:Password"];

if (!string.IsNullOrWhiteSpace(bootstrapUsername) &&
    !string.IsNullOrWhiteSpace(bootstrapPassword))
{
    using var scope = app.Services.CreateScope();

    var operatorAuthService =
        scope.ServiceProvider
            .GetRequiredService<OperatorAuthService>();

    await operatorAuthService.EnsureInitialOperatorAsync(
        bootstrapUsername,
        bootstrapPassword);
}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(
        "/Error",
        createScopeForErrors: true);

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
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
app.MapOperatorEndpoints();
app.MapCustomerRemoteAuthEndpoints();
app.MapRemoteDiagnosticCommandEndpoints();

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
