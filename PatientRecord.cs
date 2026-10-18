namespace HospitalApi.Models
{
    public class PatientRecord
    {
        public int Id { get; set; }
        public int PatientUserId { get; set; }
        public User? Patient { get; set; }
        public int DoctorUserId { get; set; }
        public User? Doctor { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime RecordDate { get; set; } = DateTime.UtcNow;
    }
}
