using System.ComponentModel.DataAnnotations;

namespace HospitalApi.DTOs
{
    public class LoginRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool EmailConfirmed { get; set; }
    }

    public class RegisterRequest
    {
        [Required, MinLength(3)] public string FullName { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, MinLength(6)] public string Password { get; set; } = string.Empty;

        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone number must be exactly 11 digits.")]
        public string? Phone { get; set; }

        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? Address { get; set; }

        // Under-18 (item v)
        public bool IsMinor { get; set; }
        public string? GuardianName { get; set; }
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Guardian phone must be exactly 11 digits.")]
        public string? GuardianPhone { get; set; }
        [EmailAddress] public string? GuardianEmail { get; set; }
        public string? GuardianRelationship { get; set; }
        public bool GuardianConsent { get; set; }
        [RegularExpression(@"^\d{11}$", ErrorMessage = "NIN must be exactly 11 digits.")]
        public string? NIN { get; set; }
        public bool NINVerified { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required] public string Token { get; set; } = string.Empty;
        [Required, MinLength(6)] public string NewPassword { get; set; } = string.Empty;
    }

    public class ConfirmEmailRequest
    {
        [Required] public string Token { get; set; } = string.Empty;
    }

    public class CreateUserRequest
    {
        [Required, MinLength(3)] public string FullName { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
        [Required] public Models.UserRole Role { get; set; }
        [RegularExpression(@"^\d{11}$", ErrorMessage = "Phone number must be exactly 11 digits.")]
        public string? Phone { get; set; }
        public string? Specialization { get; set; }
        public string? Bio { get; set; }
        public decimal? ConsultationFee { get; set; }
        public int? SlotDurationMinutes { get; set; }
        public int? FacilityId { get; set; }
    }

    public class ToggleActiveRequest { public bool IsActive { get; set; } }

    public class ToggleAvailabilityRequest { public bool IsAcceptingBookings { get; set; } public string? Note { get; set; } }

    public class CreateFacilityRequest { public string Name { get; set; } = ""; public string? Address { get; set; } public string? Phone { get; set; } }
    public class CreateDrugRequest { public string Name { get; set; } = ""; public string? Form { get; set; } public string? Strength { get; set; } }

    public class CreateScheduleRequest { public int DayOfWeek { get; set; } public TimeSpan StartTime { get; set; } public TimeSpan EndTime { get; set; } }
    public class CreateTimeOffRequest { public string Date { get; set; } = ""; public string? StartTime { get; set; } public string? EndTime { get; set; } public string? Reason { get; set; } }

    public class CreateAppointmentRequest
    {
        public int DoctorUserId { get; set; }
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        public string Reason { get; set; } = "";
    }
    public class RespondAppointmentRequest { public string Status { get; set; } = ""; public string? DoctorNote { get; set; } }
    public class RescheduleAppointmentRequest { public string Date { get; set; } = ""; public string Time { get; set; } = ""; }

    public class ConfirmPaymentRequest { public bool Succeed { get; set; } }

    public class CreatePrescriptionRequest
    {
        public int PatientUserId { get; set; }
        public int? AppointmentId { get; set; }
        public int? DrugId { get; set; }
        public string Medication { get; set; } = "";
        public string Dosage { get; set; } = "";
        public string? Instructions { get; set; }
        public int? Quantity { get; set; }
    }

    public class CreateLabOrderRequest { public int PatientUserId { get; set; } public string TestName { get; set; } = ""; public string? Notes { get; set; } }
    public class SubmitLabResultRequest { public string ResultText { get; set; } = ""; }

    public class CreatePatientRecordRequest { public int PatientUserId { get; set; } public string Notes { get; set; } = ""; }

    // Item ix: complaint may optionally reference a doctor
    public class CreateComplaintRequest
    {
        public string Subject { get; set; } = "";
        public string Message { get; set; } = "";
        public int? DoctorUserId { get; set; }
    }
    public class RespondComplaintRequest { public string Status { get; set; } = ""; public string? AdminResponse { get; set; } }

    public class UpdateProfileRequest
    {
        public string FullName { get; set; } = "";
        public string? Phone { get; set; }
        public string? MedicalHistory { get; set; }
        public string? Bio { get; set; }
        public decimal? ConsultationFee { get; set; }
        public int? SlotDurationMinutes { get; set; }
        public int? FacilityId { get; set; }
    }
}