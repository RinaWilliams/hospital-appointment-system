namespace HospitalApi.Models
{
    // Blocks a doctor's availability for a specific date, either the whole
    // day (StartTime/EndTime both null) or a partial window within it.
    public class DoctorTimeOff
    {
        public int Id { get; set; }
        public int DoctorUserId { get; set; }
        public User? Doctor { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string? Reason { get; set; }
    }
}
