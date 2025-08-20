using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DeviceDB.Models;

public partial class MainDBContext : DbContext
{
    public virtual DbSet<DeviceConfig> DeviceConfigs { get; set; }

    public virtual DbSet<EncryptedTestSequence> EncryptedTestSequences { get; set; }

    public virtual DbSet<TestRecord> TestRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceConfig>(entity =>
        {
            entity.HasKey(e => e.ConfigId);

            entity.ToTable("DeviceConfig");

            entity.Property(e => e.ConfigId).HasColumnName("ConfigID");
        });

        modelBuilder.Entity<EncryptedTestSequence>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("EncryptedTestSequence");
        });

        modelBuilder.Entity<TestRecord>(entity =>
        {
            entity.HasKey(e => e.Time);

            entity.ToTable("TestRecord");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
