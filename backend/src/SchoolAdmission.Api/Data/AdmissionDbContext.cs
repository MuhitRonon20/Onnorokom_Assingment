using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data.Entities;

namespace SchoolAdmission.Api.Data;

public class AdmissionDbContext : DbContext
{
    public AdmissionDbContext(DbContextOptions<AdmissionDbContext> options)
        : base(options)
    {
    }

    public DbSet<AdmissionClass> Classes => Set<AdmissionClass>();
    public DbSet<AdmissionHold> Holds => Set<AdmissionHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AdmissionClass configuration
        modelBuilder.Entity<AdmissionClass>(entity =>
        {
            entity.ToTable("Classes");

            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasMaxLength(32);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.Property(c => c.TotalSeats).IsRequired();

            entity.HasIndex(c => c.Name).IsUnique();

            entity.ToTable(t => t.HasCheckConstraint("CK_Classes_TotalSeats_Positive", "\"TotalSeats\" > 0"));
        });

        // AdmissionHold configuration
        modelBuilder.Entity<AdmissionHold>(entity =>
        {
            entity.ToTable("Holds");

            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).HasMaxLength(32);
            entity.Property(h => h.ClassId).IsRequired().HasMaxLength(32);
            entity.Property(h => h.StudentId).IsRequired().HasMaxLength(17);
            entity.Property(h => h.Status).IsRequired().HasMaxLength(20);

            entity.HasOne(h => h.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(h => h.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(h => new { h.ClassId, h.Status });
            entity.HasIndex(h => h.StudentId);
            entity.HasIndex(h => new { h.Status, h.ExpiresAtUtc });

            // Partial unique index for confirmed admissions: a student can have at most one confirmed admission per class
            entity.HasIndex(h => new { h.StudentId, h.ClassId })
                .HasDatabaseName("IX_Holds_Student_Class_Confirmed")
                .HasFilter("\"Status\" = 'CONFIRMED'")
                .IsUnique();
        });
    }
}

