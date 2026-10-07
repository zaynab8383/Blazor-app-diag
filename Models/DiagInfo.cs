using System;
using System.Collections.Generic;

namespace MyBlazorApp1.Models;

public partial class DiagInfo
{
    public int Id { get; set; }

    public string SerialNumber { get; set; } = null!;

    public string OwnerPhoneNumber { get; set; } = null!;

    public string? InstallationId { get; set; }

    public string? DeviceTokenHash { get; set; }

    public string? PairingCodeHash { get; set; }

    public int AuthVersion { get; set; }

    public bool IsBlocked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
