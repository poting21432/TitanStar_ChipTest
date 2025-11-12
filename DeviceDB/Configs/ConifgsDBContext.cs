using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
namespace DeviceDB;

public partial class ConifgsDBContext : DbContext
{
    public ConifgsDBContext(DbContextOptions<ConifgsDBContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Config> Configs { get; set; }

    public virtual DbSet<PLCAddr> PlCAddrs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Config>(entity =>
        {
            entity.Property(e => e.Id).HasColumnName("ID");
        });

        modelBuilder.Entity<PLCAddr>(entity =>
        {
            entity.ToTable("PLCAddr");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.StationId).HasColumnName("StationID");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
