namespace HospitalApi.Models
{
    public class DoctorProfile
    {
        public int UserId { get; set; }
        public User? User { get; set; }
        public string Specialization { get; set; } = string.Empty;
        public string? LicenseNumber { get; set; }
        public string? Bio { get; set; }
        public decimal ConsultationFee { get; set; } = 5000.00m;
        public int SlotDurationMinutes { get; set; } = 30;
        public int? FacilityId { get; set; }
        public Facility? Facility { get; set; }

        // Item xii: doctor must explicitly turn this on
        public bool IsAcceptingBookings { get; set; } = false;
        public string? AvailabilityNote { get; set; }
    }

    public enum Gender { Male, Female, Other }

    public class PatientProfile
    {
        public int UserId { get; set; }
        public User? User { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public Gender? Gender { get; set; }
        public string? Address { get; set; }
        public string? MedicalHistory { get; set; }

        // Under-18 (item v)
        public bool IsMinor { get; set; } = false;
        public string? GuardianName { get; set; }
        public string? GuardianPhone { get; set; }
        public string? GuardianEmail { get; set; }
        public string? GuardianRelationship { get; set; }
        public bool? GuardianConsent { get; set; }

        // NIN verification (item v)
        public string? NIN { get; set; }
        public bool NINVerified { get; set; } = false;
        public DateTime? NINVerifiedAt { get; set; }
    }
}