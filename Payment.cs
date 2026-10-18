namespace HospitalApi.Models
{
    public enum PaymentStatus { Pending, Paid, Failed }

    public class Payment
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }
        public int PatientUserId { get; set; }
        public User? Patient { get; set; }
        public decimal Amount { get; set; }
        public string Reference { get; set; } = string.Empty;
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }
}
