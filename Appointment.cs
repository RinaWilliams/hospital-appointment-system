namespace HospitalApi.Models
{
    public enum AppointmentStatus { PendingPayment, Pending, Approved, Completed, Rejected, Cancelled }

    public class Appointment
    {
        public int Id { get; set; }
        public int PatientUserId { get; set; }
        public User? Patient { get; set; }
        public int DoctorUserId { get; set; }
        public User? Doctor { get; set; }
        public DateTime AppointmentDate { get; set; }
        public TimeSpan AppointmentTime { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? DoctorNote { get; set; }
        public decimal FeeAmount { get; set; }
        public AppointmentStatus Status { get; set; } = AppointmentStatus.PendingPayment;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Item xi: 5-minute reminder tracking
        public bool Reminder5MinSent { get; set; } = false;
        public DateTime? Reminder5MinAt { get; set; }

        public Payment? Payment { get; set; }
    }
}