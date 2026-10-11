namespace MyBlazorApp1.Models;

public class RemoteDiagnosticSession
{
    public int Id { get; set; }

    public int OperatorAccountId { get; set; }

    public int DiagInfoId { get; set; }

    public string SessionTokenHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}