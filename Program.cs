using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Api;
using MyBlazorApp1.Components;
using MyBlazorApp1.Data;
using MyBlazorApp1.Services;
using MyBlazorApp1.Api;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


// =========================================================
// SERVICES
// =========================================================

builder.Services.AddSingleton<UdpServer>();

builder.Services.AddSingleton<DiagnosticTcpServerService>();

builder.Services.AddSingleton<DiagnosticService>();

builder.Services.AddSingleton<DiagnosticMenuService>();

builder.Services.AddScoped<DiagDeviceService>();

builder.Services.AddScoped<DeviceAuthService>();
builder.Services.AddScoped<OtpService>();

// =========================================================
// SERIAL
// =========================================================


builder.Services.AddSingleton<SerialService>();

//builder.Services.AddDbContext<DiagDbContext>(options =>
//    options.UseSqlServer(
//        builder.Configuration.GetConnectionString("DiagConnection")));

builder.Services.AddDbContextFactory<DiagDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DiagConnection")));
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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapAuthEndpoints();

app.Run();