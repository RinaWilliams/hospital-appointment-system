using System.Text;
using HospitalApi.Models;

namespace HospitalApi.Services
{
    
    public static class IcsBuilder
    {
        public static string Escape(string? s) =>
           (s ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
                     .Replace("\r\n", "\\n").Replace("\n", "\\n");

        
        private static void AppendLine(StringBuilder sb, string line)
        {
            var bytes = Encoding.UTF8.GetByteCount(line);
            if (bytes <= 75) { sb.Append(line).Append("\r\n"); return; }

            var current = new StringBuilder();
            int currentBytes = 0;
            int limit = 75;
            foreach (var rune in line.EnumerateRunes())
            {
                var r = rune.ToString();
                int rb = Encoding.UTF8.GetByteCount(r);
                if (currentBytes + rb > limit)
                {
                    sb.Append(current).Append("\r\n");
                    current.Clear();
                    current.Append(' ');
                    currentBytes = 1;
                    limit = 75;
                }
                current.Append(r);
                currentBytes += rb;
            }
            sb.Append(current).Append("\r\n");
        }

        public static StringBuilder BeginCalendar(string calendarName, bool subscription)
        {
            var sb = new StringBuilder();
            AppendLine(sb, "BEGIN:VCALENDAR");
            AppendLine(sb, "VERSION:2.0");
            AppendLine(sb, "PRODID:-//Hospital Appointment System//Appointment Reminders//EN");
            AppendLine(sb, "CALSCALE:GREGORIAN");
            AppendLine(sb, "METHOD:PUBLISH");
            AppendLine(sb, "X-WR-CALNAME:" + Escape(calendarName));
            if (subscription)
            {
                // Ask calendar apps to re-check the feed every hour.
                AppendLine(sb, "REFRESH-INTERVAL;VALUE=DURATION:PT1H");
                AppendLine(sb, "X-PUBLISHED-TTL:PT1H");
            }
            return sb;
        }

        public static void AppendEvent(StringBuilder sb, Appointment appt, bool forPatient)
        {
            var start = appt.AppointmentDate.Date + appt.AppointmentTime;
            int minutes = appt.Doctor?.DoctorProfile?.SlotDurationMinutes ?? 30;
            if (minutes < 5) minutes = 30;
            var end = start.AddMinutes(minutes);

            string other = forPatient ? (appt.Doctor?.FullName ?? "your doctor") : (appt.Patient?.FullName ?? "patient");
            string title = forPatient ? "Hospital appointment with " + other : "Patient appointment: " + other;

            var facility = appt.Doctor?.DoctorProfile?.Facility;
            string? location = facility == null ? null
                : string.Join(", ", new[] { facility.Name, facility.Address }.Where(x => !string.IsNullOrWhiteSpace(x)));

            string description =
                $"Specialty: {appt.Doctor?.DoctorProfile?.Specialization ?? "N/A"}\n" +
                $"Reason: {appt.Reason}\n" +
                $"Status: {appt.Status}" +
                (appt.Status == AppointmentStatus.Pending ? " (waiting for the doctor to confirm)" : "") +
                (forPatient ? "\nPlease arrive 10 minutes early." : "");

            AppendLine(sb, "BEGIN:VEVENT");
            AppendLine(sb, $"UID:appointment-{appt.Id}@hospital-appointment-system");
            AppendLine(sb, "DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"));
            // Floating local time: appears at the same clock time on the phone.
            AppendLine(sb, "DTSTART:" + start.ToString("yyyyMMdd'T'HHmmss"));
            AppendLine(sb, "DTEND:" + end.ToString("yyyyMMdd'T'HHmmss"));
            AppendLine(sb, "SUMMARY:" + Escape(title));
            AppendLine(sb, "DESCRIPTION:" + Escape(description));
            if (!string.IsNullOrEmpty(location)) AppendLine(sb, "LOCATION:" + Escape(location));
            AppendLine(sb, "STATUS:" + (appt.Status == AppointmentStatus.Approved ? "CONFIRMED" : "TENTATIVE"));
            AppendLine(sb, "TRANSP:OPAQUE");

            AppendLine(sb, "BEGIN:VALARM");
            AppendLine(sb, "TRIGGER:-P1D");
            AppendLine(sb, "ACTION:DISPLAY");
            AppendLine(sb, "DESCRIPTION:Appointment tomorrow - " + Escape(title));
            AppendLine(sb, "END:VALARM");

            AppendLine(sb, "BEGIN:VALARM");
            AppendLine(sb, "TRIGGER:-PT1H");
            AppendLine(sb, "ACTION:DISPLAY");
            AppendLine(sb, "DESCRIPTION:Appointment in 1 hour - " + Escape(title));
            AppendLine(sb, "END:VALARM");

            AppendLine(sb, "END:VEVENT");
        }

        public static string EndCalendar(StringBuilder sb)
        {
            AppendLine(sb, "END:VCALENDAR");
            return sb.ToString();
        }

        public static string SingleAppointment(Appointment appt, bool forPatient)
        {
            var sb = BeginCalendar("Hospital Appointment", false);
            AppendEvent(sb, appt, forPatient);
            return EndCalendar(sb);
        }
    }
}
