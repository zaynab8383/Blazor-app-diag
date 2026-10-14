
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Data;
using MyBlazorApp1.Services;

namespace MyBlazorApp1.Api;

public static class CustomerRemoteDiagnosticCommandEndpoints
{
    public static void MapCustomerRemoteDiagnosticCommandEndpoints(
        this WebApplication app)
    {
        app.MapPost(
            "/api/customer/remote-diagnostic/send-command",
            async (
                CustomerSendCommandRequest request,
                ClaimsPrincipal user,
                IDbContextFactory<DiagDbContext> dbFactory,
                OnlineDiagnosticDiscoveryService discovery,
                DiagnosticCommandDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                if (user.Identity?.IsAuthenticated != true ||
                    !user.IsInRole("Customer"))
                {
                    return Results.Unauthorized();
                }

                var phone = user.FindFirstValue(ClaimTypes.MobilePhone);

                if (string.IsNullOrWhiteSpace(phone))
                    return Results.Unauthorized();

                if (string.IsNullOrWhiteSpace(request.SerialNumber) ||
                    string.IsNullOrWhiteSpace(request.Command))
                {
                    return Results.BadRequest(new
                    {
                        success = false,
                        message = "Serial number and command are required."
                    });
                }

                var serial = request.SerialNumber.Trim();
                var command = request.Command.Trim();

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

                await using var db =
                    await dbFactory.CreateDbContextAsync(cancellationToken);

                var owned = await db.DiagInfos
                    .AsNoTracking()
                    .Where(x =>
                        x.OwnerPhoneNumber == phone &&
                        x.SerialNumber == serial)
                    .Select(x => new
                    {
                        x.SerialNumber,
                        x.IsBlocked
                    })
                    .SingleOrDefaultAsync(cancellationToken);

                if (owned == null)
                    return Results.Forbid();

                if (owned.IsBlocked)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "The diagnostic device is blocked."
                    });
                }

                var matches = discovery.GetOnlineDevices()
                    .Where(x => string.Equals(
                        x.SerialNumber,
                        serial,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (matches.Length == 0)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message = "The diagnostic device is offline."
                    });
                }

                if (matches.Length > 1)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message =
                            "Multiple active connections report this serial number."
                    });
                }

                try
                {
                    var response = await dispatcher.ExecuteAsync(
                        matches[0].ConnectionId,
                        command,
                        TimeSpan.FromSeconds(15),
                        cancellationToken);

                    return Results.Ok(new
                    {
                        success = true,
                        serialNumber = serial,
                        command,
                        response
                    });
                }
                catch (TimeoutException)
                {
                    return Results.Json(
                        new
                        {
                            success = false,
                            message =
                                "Timed out waiting for the diagnostic response."
                        },
                        statusCode: 504);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    return Results.Json(
                        new
                        {
                            success = false,
                            message = "Request was cancelled."
                        },
                        statusCode: 499);
                }
                catch (Exception ex) when (
                    ex is IOException or
                    InvalidOperationException or
                    System.Net.Sockets.SocketException)
                {
                    return Results.Conflict(new
                    {
                        success = false,
                        message =
                            "Could not complete the command on this diagnostic connection."
                    });
                }
            })
            .RequireAuthorization(
                policy => policy.RequireRole("Customer"));
    }

    public sealed record CustomerSendCommandRequest(
        string SerialNumber,
        string Command);
}
