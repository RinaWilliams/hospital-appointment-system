namespace HospitalApi.Models
{
    public enum ComplaintStatus { Open, InReview, Resolved }

    public class Complaint
    {
        public int Id { get; set; }
        public int PatientUserId { get; set; }
        public User? Patient { get; set; }

        // Item ix: which doctor this complaint is about (optional)
        public int? DoctorUserId { get; set; }
        public User? Doctor { get; set; }

        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public ComplaintStatus Status { get; set; } = ComplaintStatus.Open;
        public string? AdminResponse { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}