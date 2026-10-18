namespace HospitalApi.Models
{
    // DayOfWeek 0=Sunday..6=Saturday, matching System.DayOfWeek, to make slot math trivial.
    public class DoctorSchedule
    {
        public int Id { get; set; }
        public int DoctorUserId { get; set; }
        public User? Doctor { get; set; }
        public int DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }
}
