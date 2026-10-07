using System;
using System.Collections.Generic;

namespace MyBlazorApp1.Models;

public partial class Otp
{
    public int Id { get; set; }

    public int DiagInfoId { get; set; }

    public string CodeHash { get; set; } = null!;

    public string Purpose { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
