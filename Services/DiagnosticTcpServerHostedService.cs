
using Microsoft.Extensions.Hosting;

namespace MyBlazorApp1.Services;

public sealed class DiagnosticTcpServerHostedService : IHostedService
{
    //private readonly DiagnosticTcpServerService _tcpServer;

    //public DiagnosticTcpServerHostedService(
    //    DiagnosticTcpServerService tcpServer)
    //{
    //    _tcpServer = tcpServer;
    //}

    //public Task StartAsync(CancellationToken cancellationToken)
    //{
    //    cancellationToken.ThrowIfCancellationRequested();

    //    return _tcpServer.StartAsync(9000);
    //}
    private readonly DiagnosticTcpServerService _tcpServer;

    public DiagnosticTcpServerHostedService(
        DiagnosticTcpServerService tcpServer,
        OnlineDiagnosticDiscoveryService discovery)
    {
        _tcpServer = tcpServer;
        _ = discovery; // اطمینان از ساخته‌شدن Discovery پیش از شروع سرور
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "[HOSTED TCP] DiagnosticTcpServerHostedService.StartAsync called.");

        cancellationToken.ThrowIfCancellationRequested();

        return _tcpServer.StartAsync(9000);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _tcpServer.Stop();
        return Task.CompletedTask;
    }
}
