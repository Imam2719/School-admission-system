using Microsoft.EntityFrameworkCore;

namespace SchoolAdmission.Api.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Hold> Holds => Set<Hold>();
    public DbSet<Application> Applications => Set<Application>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<SchoolClass>(e =>
        {
            e.ToTable("classes", t => t.HasCheckConstraint("ck_classes_total_seats", "\"TotalSeats\" > 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(40);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        mb.HasSequence<long>("student_id_seq");
        mb.Entity<Application>(e =>
        {
            e.ToTable("applications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(40);
            e.Property(x => x.GuardianName).HasMaxLength(100).IsRequired();
            e.Property(x => x.StudentName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Nid).HasMaxLength(20).IsRequired();
            e.Property(x => x.StudentId).HasMaxLength(14).IsRequired();
            e.Property(x => x.ClassId).HasMaxLength(40).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.HoldId).HasMaxLength(40);
            e.HasIndex(x => x.Nid);
            e.HasIndex(x => new { x.StudentId, x.ClassId }).IsUnique()
                .HasFilter("\"Status\" IN ('PENDING','APPROVED')").HasDatabaseName("ux_applications_one_open_per_student_class");
        });

        mb.Entity<Hold>(e =>
        {
            e.ToTable("holds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(40);
            e.Property(x => x.ClassId).HasMaxLength(40).IsRequired();
            e.Property(x => x.StudentId).HasMaxLength(64).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasOne<SchoolClass>().WithMany().HasForeignKey(x => x.ClassId);
            e.HasIndex(x => new { x.ClassId, x.Status });
            e.HasIndex(x => x.StudentId);

            // DB-level guard: a student can have only one active (HELD/CONFIRMED) application per class.
            e.HasIndex(x => new { x.ClassId, x.StudentId })
                .IsUnique()
                .HasFilter("\"Status\" IN ('HELD','CONFIRMED')")
                .HasDatabaseName("ux_holds_one_active_per_student_class");
        });
    }
}
