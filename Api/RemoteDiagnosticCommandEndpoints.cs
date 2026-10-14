
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;
using MyBlazorApp1.Services;
using System.Net.Sockets;

namespace MyBlazorApp1.Api;

public static class RemoteDiagnosticCommandEndpoints
{
    public static void MapRemoteDiagnosticCommandEndpoints(
        this WebApplication app)
    {
        app.MapPost(
            "/api/operator/remote-diagnostic/send-command",
            async (
                SendDiagnosticCommandRequest request,
                HttpContext http,
                IDbContextFactory<DiagDbContext> dbFactory,
                OnlineDiagnosticDiscoveryService discovery,
                DiagnosticTcpServerService tcpServer,
                CancellationToken cancellationToken) =>
            {
                // این API فقط برای اپراتور احراز هویت‌شده است.
                if (http.User.Identity?.IsAuthenticated != true ||
                    !http.User.IsInRole("Operator"))
                {
                    return Results.Unauthorized();
                }

                if (string.IsNullOrWhiteSpace(request.SessionToken) ||
                    string.IsNullOrWhiteSpace(request.SerialNumber) ||
                    string.IsNullOrWhiteSpace(request.Command))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Session token, serial number and command are required."
                    });
                }

                // فعلاً فقط فرمان‌های عددی و checkbox مجازند.
                // نمونه‌ها: 0، 13، -1، 3,1 و 3,0
                var command = request.Command.Trim();

                //if (command.Length > 32 ||
                //    !Regex.IsMatch(
                //        command,
                //        @"^-?\d+(?:,[01])?$",
                //        RegexOptions.CultureInvariant))
                //{
                //    return Results.BadRequest(new
                //    {
                //        success = false,
                //        message = "Invalid diagnostic command format."
                //    });
                //}


                if (command.Length > 32 ||
                    !Regex.IsMatch(
                        command,
                        @"^(?:-?\d+(?:,[01])?|-1-1-1|-2-2-2)$",
                        RegexOptions.CultureInvariant))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Invalid diagnostic command format."
                    });
                }

                byte[] tokenBytes;

                try
                {
                    tokenBytes =
                        Convert.FromBase64String(request.SessionToken);
                }
                catch (FormatException)
                {
                    return Results.Unauthorized();
                }

                if (tokenBytes.Length != 32)
                    return Results.Unauthorized();

                var tokenHash = Convert.ToHexString(
                    SHA256.HashData(tokenBytes));

                CryptographicOperations.ZeroMemory(tokenBytes);

                await using var db =
                    await dbFactory.CreateDbContextAsync(
                        cancellationToken);

                // نشست باید معتبر، منقضی‌نشده و لغونشده باشد.
                var session = await db.RemoteDiagnosticSessions
                    .SingleOrDefaultAsync(
                        x => x.SessionTokenHash == tokenHash &&
                             x.RevokedAt == null &&
                             x.ExpiresAt > DateTime.UtcNow,
                        cancellationToken);

                if (session == null)
                    return Results.Unauthorized();

                // نشست به اپراتور فعلی تعلق داشته باشد.
                var username = http.User.Identity?.Name;

                if (string.IsNullOrWhiteSpace(username))
                    return Results.Unauthorized();

                var operatorAccount = await db.OperatorAccounts
                    .SingleOrDefaultAsync(
                        x => x.Id == session.OperatorAccountId &&
                             x.Username == username &&
                             x.IsActive,
                        cancellationToken);

                if (operatorAccount == null)
                    return Results.Unauthorized();

                var serialNumber = request.SerialNumber.Trim();

                var device = await db.DiagInfos
                    .SingleOrDefaultAsync(
                        x => x.Id == session.DiagInfoId &&
                             x.SerialNumber == serialNumber,
                        cancellationToken);

                if (device == null)
                {
                    return Results.Forbid();
                }

                if (device.IsBlocked)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "The diagnostic device is blocked."
                    });
                }

                // شماره سریال باید به یک اتصال آنلاین مشخص نگاشت شود.
                var matches = discovery.GetOnlineDevices()
                    .Where(x => string.Equals(
                        x.SerialNumber,
                        device.SerialNumber,
                        StringComparison.Ordinal))
                    .ToArray();

                if (matches.Length == 0)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "The diagnostic device is offline."
                    });
                }

                // اگر سریال روی چند اتصال دیده شود، حدس نمی‌زنیم.
                if (matches.Length > 1)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "Multiple active connections reported this serial number."
                    });
                }

                var identity = matches[0];

                //try
                //{
                //    var bytes = Encoding.UTF8.GetBytes(command + "\n");

                //    // مهم: ارسال به همان اتصال، نه Primary Client.
                //    await tcpServer.SendAsync(
                //        identity.ConnectionId,
                //        bytes);

                //    return Results.Ok(new
                //    {
                //        success = true,
                //        serialNumber = device.SerialNumber,
                //        message = "Command sent to the selected connection."
                //    });
                //}
                //catch (InvalidOperationException)
                //{
                //    return Results.Conflict(new
                //    {
                //        success = false,
                //        message = "The diagnostic connection is no longer available."
                //    });
                //}
                //catch (IOException)
                //{
                //    return Results.Conflict(new
                //    {
                //        success = false,
                //        message = "The diagnostic connection failed during sending."
                //    });
                //}
                try
                {
                    var bytes = Encoding.UTF8.GetBytes(command + "\n");

                    // Send only to the connection identified for this device.
                    await tcpServer.SendAsync(
                        identity.ConnectionId,
                        bytes);

                    return Results.Ok(new
                    {
                        success = true,
                        serialNumber = device.SerialNumber,
                        message = "Command sent to the selected connection."
                    });
                }
                catch (ObjectDisposedException)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "The diagnostic connection is no longer available."
                    });
                }
                catch (InvalidOperationException)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "The diagnostic connection is no longer available."
                    });
                }
                catch (IOException)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "The diagnostic connection failed during sending."
                    });
                }
                catch (SocketException)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "The diagnostic connection failed during sending."
                    });
                }
            })
            .RequireAuthorization(policy =>
                policy.RequireRole("Operator"));
    }

    public sealed record SendDiagnosticCommandRequest(
        string SessionToken,
        string SerialNumber,
        string Command);
}

