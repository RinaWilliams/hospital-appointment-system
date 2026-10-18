using HospitalApi.Data;
using HospitalApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Services
{
    public class SlotService
    {
        public const int PatientChangeCutoffHours = 24;

        private readonly AppDbContext _db;
        public SlotService(AppDbContext db) => _db = db;

        public async Task<List<TimeSpan>> GetAvailableSlotsAsync(int doctorUserId, DateOnly date, int? excludeAppointmentId = null)
        {
            var doctorProfile = await _db.DoctorProfiles.FindAsync(doctorUserId);
            if (doctorProfile == null) return new List<TimeSpan>();

            // If the doctor has turned off availability, return nothing.
            if (!doctorProfile.IsAcceptingBookings) return new List<TimeSpan>();

            int slotMinutes = doctorProfile.SlotDurationMinutes > 0 ? doctorProfile.SlotDurationMinutes : 30;

            int dayOfWeek = (int)date.DayOfWeek;
            var windows = await _db.DoctorSchedules
                .Where(s => s.DoctorUserId == doctorUserId && s.DayOfWeek == dayOfWeek)
                .ToListAsync();

            if (windows.Count == 0) return new List<TimeSpan>();

            var candidates = new List<TimeSpan>();
            foreach (var w in windows)
            {
                for (var t = w.StartTime; t + TimeSpan.FromMinutes(slotMinutes) <= w.EndTime; t += TimeSpan.FromMinutes(slotMinutes))
                    candidates.Add(t);
            }

            var dateAsDateTime = date.ToDateTime(TimeOnly.MinValue);
            var bookedTimes = await _db.Appointments
                .Where(a => a.DoctorUserId == doctorUserId
                            && a.AppointmentDate.Date == dateAsDateTime.Date
                            && a.Status != AppointmentStatus.Cancelled
                            && a.Status != AppointmentStatus.Rejected
                            && (excludeAppointmentId == null || a.Id != excludeAppointmentId))
                .Select(a => a.AppointmentTime)
                .ToListAsync();

            var timeOffs = await _db.DoctorTimeOffs
                .Where(t => t.DoctorUserId == doctorUserId && DateOnly.FromDateTime(t.Date) == date)
                .ToListAsync();

            bool IsBlocked(TimeSpan slotStart)
            {
                var slotEnd = slotStart + TimeSpan.FromMinutes(slotMinutes);
                foreach (var t in timeOffs)
                {
                    if (t.StartTime == null || t.EndTime == null) return true;
                    if (slotStart < t.EndTime && slotEnd > t.StartTime) return true;
                }
                return false;
            }

            // ===== Strict past-time filter =====
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            var nowTime = now.TimeOfDay;

            return candidates
                .Where(c => !bookedTimes.Contains(c) && !IsBlocked(c))
                .Where(c =>
                {
                    if (date < today) return false;
                    if (date == today && c <= nowTime) return false;
                    return true;
                })
                .OrderBy(c => c)
                .ToList();
        }

        public static bool IsBeyondChangeCutoff(DateTime appointmentDate, TimeSpan appointmentTime)
        {
            var apptDateTime = appointmentDate.Date + appointmentTime;
            return apptDateTime > DateTime.Now.AddHours(PatientChangeCutoffHours);
        }
    }
}