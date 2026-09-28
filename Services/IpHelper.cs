using System.Net.Sockets;
using System.Net;
using System.Text;

public static class IpHelper
{
    private const string SERVER_IP = "2a01:4f9:6a:205e::1";
    private const int SERVER_PORT = 5000;

    public static async Task<string> GetPublicIPv6Async()
    {
        try
        {
            using var client = new Socket(
                AddressFamily.InterNetworkV6,
                SocketType.Stream,
                ProtocolType.Tcp)
            {
                DualMode = false,
                ReceiveTimeout = 7000,     // optional: prevent hanging forever
                SendTimeout = 5000
            };

            var endpoint = new IPEndPoint(IPAddress.Parse(SERVER_IP), SERVER_PORT);

            await client.ConnectAsync(endpoint);

            byte[] buffer = new byte[2048];
            int received = await client.ReceiveAsync(buffer, SocketFlags.None);

            if (received > 0)
            {
                return Encoding.UTF8.GetString(buffer, 0, received).Trim();
            }

            return "No response from server";
        }
        catch (SocketException ex)
        {
            return $"Connection failed: {ex.Message} (Error: {ex.ErrorCode})";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}