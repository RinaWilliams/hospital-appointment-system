using HospitalApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();
        public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
        public DbSet<Facility> Facilities => Set<Facility>();
        public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
        public DbSet<DoctorTimeOff> DoctorTimeOffs => Set<DoctorTimeOff>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<Drug> Drugs => Set<Drug>();
        public DbSet<Prescription> Prescriptions => Set<Prescription>();
        public DbSet<LabOrder> LabOrders => Set<LabOrder>();
        public DbSet<PatientRecord> PatientRecords => Set<PatientRecord>();
        public DbSet<Complaint> Complaints => Set<Complaint>();
        public DbSet<Payment> Payments => Set<Payment>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasIndex(u => u.Email).IsUnique();
                e.HasIndex(u => u.EmailConfirmToken).IsUnique();
                e.HasIndex(u => u.PasswordResetToken).IsUnique();
                e.HasIndex(u => u.CalendarToken).IsUnique();
                e.Property(u => u.Role).HasConversion<string>();
                e.Property(u => u.IsActive).HasDefaultValue(true);
                e.Property(u => u.EmailConfirmed).HasDefaultValue(false);
                e.Property(u => u.Phone).HasMaxLength(11);
                e.Property(u => u.CalendarToken).HasMaxLength(64);
                e.Property(u => u.EmailConfirmToken).HasMaxLength(64);
                e.Property(u => u.PasswordResetToken).HasMaxLength(64);
            });

            mb.Entity<DoctorProfile>(e =>
            {
                e.ToTable("DoctorProfiles");
                e.HasKey(d => d.UserId);
                e.HasOne(d => d.User).WithOne(u => u.DoctorProfile)
                    .HasForeignKey<DoctorProfile>(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(d => d.Facility).WithMany().HasForeignKey(d => d.FacilityId).OnDelete(DeleteBehavior.SetNull);
                e.Property(d => d.ConsultationFee).HasColumnType("decimal(10,2)");
                e.Property(d => d.IsAcceptingBookings).HasDefaultValue(false);
            });

            mb.Entity<PatientProfile>(e =>
            {
                e.ToTable("PatientProfiles");
                e.HasKey(p => p.UserId);
                e.Property(p => p.Gender).HasConversion<string>();
                e.Property(p => p.GuardianPhone).HasMaxLength(11);
                e.Property(p => p.NIN).HasMaxLength(11);
                e.HasOne(p => p.User).WithOne(u => u.PatientProfile)
                    .HasForeignKey<PatientProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            mb.Entity<Facility>(e => e.ToTable("Facilities"));

            mb.Entity<DoctorSchedule>(e =>
            {
                e.ToTable("DoctorSchedules");
                e.Property(s => s.StartTime).HasColumnType("time");
                e.Property(s => s.EndTime).HasColumnType("time");
                e.HasOne(s => s.Doctor).WithMany().HasForeignKey(s => s.DoctorUserId).OnDelete(DeleteBehavior.Cascade);
            });

            mb.Entity<DoctorTimeOff>(e =>
            {
                e.ToTable("DoctorTimeOffs");
                e.Property(t => t.Date).HasColumnType("date");
                e.Property(t => t.StartTime).HasColumnType("time");
                e.Property(t => t.EndTime).HasColumnType("time");
                e.HasOne(t => t.Doctor).WithMany().HasForeignKey(t => t.DoctorUserId).OnDelete(DeleteBehavior.Cascade);
            });

            mb.Entity<Appointment>(e =>
            {
                e.ToTable("Appointments");
                e.Property(a => a.Status).HasConversion<string>();
                e.Property(a => a.FeeAmount).HasColumnType("decimal(10,2)");
                e.Property(a => a.AppointmentDate).HasColumnType("date");
                e.Property(a => a.AppointmentTime).HasColumnType("time");
                e.HasOne(a => a.Patient).WithMany().HasForeignKey(a => a.PatientUserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(a => a.Doctor).WithMany().HasForeignKey(a => a.DoctorUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(a => new { a.DoctorUserId, a.AppointmentDate });
                e.HasIndex(a => a.Status);
                e.HasIndex(a => new { a.AppointmentDate, a.AppointmentTime, a.Reminder5MinSent });
            });

            mb.Entity<Drug>(e =>
            {
                e.ToTable("Drugs");
                e.HasIndex(d => d.Name);
                e.HasIndex(d => d.IsActive);
            });

            mb.Entity<Prescription>(e =>
            {
                e.ToTable("Prescriptions");
                e.Property(p => p.Status).HasConversion<string>();
                e.HasOne(p => p.Doctor).WithMany().HasForeignKey(p => p.DoctorUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(p => p.Patient).WithMany().HasForeignKey(p => p.PatientUserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.Drug).WithMany().HasForeignKey(p => p.DrugId).OnDelete(DeleteBehavior.SetNull);
            });

            mb.Entity<LabOrder>(e =>
            {
                e.ToTable("LabOrders");
                e.Property(l => l.Status).HasConversion<string>();
                e.HasOne(l => l.Patient).WithMany().HasForeignKey(l => l.PatientUserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(l => l.Doctor).WithMany().HasForeignKey(l => l.DoctorUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(l => l.CompletedBy).WithMany().HasForeignKey(l => l.CompletedByUserId).OnDelete(DeleteBehavior.SetNull);
            });

            mb.Entity<PatientRecord>(e =>
            {
                e.ToTable("PatientRecords");
                e.HasOne(r => r.Patient).WithMany().HasForeignKey(r => r.PatientUserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(r => r.Doctor).WithMany().HasForeignKey(r => r.DoctorUserId).OnDelete(DeleteBehavior.Restrict);
            });

            mb.Entity<Complaint>(e =>
            {
                e.ToTable("Complaints");
                e.Property(c => c.Status).HasConversion<string>();
                e.HasOne(c => c.Patient).WithMany().HasForeignKey(c => c.PatientUserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Doctor).WithMany().HasForeignKey(c => c.DoctorUserId).OnDelete(DeleteBehavior.SetNull);
            });

            mb.Entity<Payment>(e =>
            {
                e.ToTable("Payments");
                e.Property(p => p.Status).HasConversion<string>();
                e.Property(p => p.Amount).HasColumnType("decimal(10,2)");
                e.HasOne(p => p.Patient).WithMany().HasForeignKey(p => p.PatientUserId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(p => p.Appointment).WithOne(a => a.Payment)
                    .HasForeignKey<Payment>(p => p.AppointmentId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}