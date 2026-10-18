namespace HospitalApi.Models
{
    public enum PrescriptionStatus { Pending, Dispensed }

    public class Prescription
    {
        public int Id { get; set; }
        public int? AppointmentId { get; set; }
        public int DoctorUserId { get; set; }
        public User? Doctor { get; set; }
        public int PatientUserId { get; set; }
        public User? Patient { get; set; }
        public int? DrugId { get; set; }
        public Drug? Drug { get; set; }
        public string Medication { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string? Instructions { get; set; }
        public int? Quantity { get; set; }
        public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Pending;
        public int? DispensedByUserId { get; set; }
        public DateTime IssuedDate { get; set; } = DateTime.UtcNow;
        public DateTime? DispensedDate { get; set; }
    }
}
