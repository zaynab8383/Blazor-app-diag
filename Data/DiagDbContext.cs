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

    public virtual DbSet<OperatorAccount> OperatorAccounts { get; set; }


    public virtual DbSet<RemoteDiagnosticSession> RemoteDiagnosticSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiagInfo>(entity =>
        {
            entity.ToTable("DiagInfo");

            entity.HasIndex(e => e.SerialNumber, "UX_DiagInfo_SerialNumber").IsUnique();

            entity.Property(e => e.DeviceTokenHash).HasMaxLength(200);
            entity.Property(e => e.InstallationId).HasMaxLength(100);
            entity.Property(e => e.OwnerPhoneNumber).HasMaxLength(20);
            entity.Property(e => e.SerialNumber).HasMaxLength(100);
        });

        modelBuilder.Entity<Otp>(entity =>
        {
            entity.ToTable("OTPs");

            entity.Property(e => e.CodeHash).HasMaxLength(200);
            entity.Property(e => e.Purpose).HasMaxLength(30);
        });


        modelBuilder.Entity<OperatorAccount>(entity =>
        {
            entity.ToTable("OperatorAccounts");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.Username).IsUnique();

            entity.Property(x => x.Username)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.PasswordHash)
                .HasMaxLength(500)
                .IsRequired();
        });

        modelBuilder.Entity<RemoteDiagnosticSession>(entity =>
        {
            entity.ToTable("RemoteDiagnosticSessions");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.SessionTokenHash)
                .IsUnique();

            entity.Property(x => x.SessionTokenHash)
                .HasMaxLength(200)
                .IsRequired();
        });
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
