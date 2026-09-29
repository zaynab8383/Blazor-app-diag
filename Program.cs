using MyBlazorApp1.Components;
using MyBlazorApp1.Services;

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


// =========================================================
// SERIAL
// =========================================================


builder.Services.AddSingleton<SerialService>();


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

app.Run();