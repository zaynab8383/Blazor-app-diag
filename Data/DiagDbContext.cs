using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MyBlazorApp1.Models;

namespace MyBlazorApp1.Data;

public partial class DiagDbContext : DbContext
{
    public DiagDbContext(DbContextOptions<DiagDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DiagInfo> DiagInfos { get; set; }

    public virtual DbSet<Otp> Otps { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiagInfo>(entity =>
        {
            entity.ToTable("DiagInfo");

            entity.HasIndex(e => e.SerialNumber, "UX_DiagInfo_SerialNumber").IsUnique();

            entity.Property(e => e.DeviceTokenHash).HasMaxLength(200);
            entity.Property(e => e.InstallationId).HasMaxLength(100);
            entity.Property(e => e.OwnerPhoneNumber).HasMaxLength(20);
            entity.Property(e => e.PairingCodeHash).HasMaxLength(200);
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
        });

        modelBuilder.Entity<Otp>(entity =>
        {
            entity.ToTable("OTPs");

            entity.Property(e => e.CodeHash).HasMaxLength(200);
            entity.Property(e => e.Purpose).HasMaxLength(30);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
