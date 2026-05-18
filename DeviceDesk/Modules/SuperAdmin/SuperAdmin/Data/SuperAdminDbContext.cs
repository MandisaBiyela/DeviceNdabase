using DeviceDesk.Modules.SuperAdmin.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.SuperAdmin.Data;

public class SuperAdminDbContext : DbContext
{
    public SuperAdminDbContext(DbContextOptions<SuperAdminDbContext> options) : base(options)
    {
    }

    public DbSet<ImportedDevice> ImportedDevices => Set<ImportedDevice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ImportedDevice>(entity =>
        {
            entity.ToTable("SuperAdmin_ImportedDevices");
            
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Serial)
                .IsUnique();

            entity.HasIndex(e => e.SchoolId);

            entity.Property(e => e.Serial)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();
        });
    }
}

