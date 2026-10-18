namespace HospitalApi.Models
{
    public enum LabOrderStatus { Ordered, Completed }

    public class LabOrder
    {
        public int Id { get; set; }
        public int PatientUserId { get; set; }
        public User? Patient { get; set; }
        public int DoctorUserId { get; set; }
        public User? Doctor { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public LabOrderStatus Status { get; set; } = LabOrderStatus.Ordered;
        public string? ResultText { get; set; }
        public int? CompletedByUserId { get; set; }
        public User? CompletedBy { get; set; }
        public DateTime RequestedDate { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
