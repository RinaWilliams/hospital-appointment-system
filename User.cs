namespace HospitalApi.Models
{
    public enum UserRole { Admin, Doctor, Patient, Pharmacist, LabTechnician }

    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public string? Phone { get; set; }        // exactly 11 digits when set
        public bool IsActive { get; set; } = true;
        public string? CalendarToken { get; set; }

        // Email confirmation (items ii & vi)
        public bool EmailConfirmed { get; set; } = false;
        public string? EmailConfirmToken { get; set; }
        public DateTime? EmailConfirmSentAt { get; set; }

        // Password reset (item iii)
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpires { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DoctorProfile? DoctorProfile { get; set; }
        public PatientProfile? PatientProfile { get; set; }
    }
}