using HospitalApi.Data;
using HospitalApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Services
{
    /// <summary>
    /// Background worker that emails the doctor 5 minutes before each approved
    /// appointment. Runs every 30 seconds. Remembers which appointments it has
    /// already reminded via Appointment.Reminder5MinSent so the doctor never
    /// receives a duplicate for the same visit.
    /// </summary>
    public class AppointmentReminderService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AppointmentReminderService> _log;

        // How far ahead of the appointment the reminder should fire.
        private static readonly TimeSpan LeadTime = TimeSpan.FromMinutes(5);

        // How often the loop runs.
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

        public AppointmentReminderService(IServiceProvider services, ILogger<AppointmentReminderService> log)
        {
            _services = services;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("AppointmentReminderService started. Polling every {Seconds}s.",
                PollInterval.TotalSeconds);

            // Small delay so the app can finish starting up before the first poll.
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    // Never let one bad iteration kill the worker.
                    _log.LogError(ex, "AppointmentReminderService iteration failed.");
                }

                try { await Task.Delay(PollInterval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }

            _log.LogInformation("AppointmentReminderService stopped.");
        }

        private async Task CheckRemindersAsync(CancellationToken ct)
        {
            // Each iteration gets its own scope so we can safely use the scoped
            // AppDbContext and EmailService inside a singleton hosted service.
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<EmailService>();

            var now = DateTime.Now;                        // server local time
            var windowEnd = now.Add(LeadTime);             // now + 5 min

            // Only look at "Approved" appointments that have not yet been reminded.
            // We compare local server time against the appointment's date + time,
            // because the app stores those as "floating local" (no timezone).
            var upcoming = await db.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .Where(a => a.Status == AppointmentStatus.Approved
                            && !a.Reminder5MinSent)
                .ToListAsync(ct);

            // Filter in-memory: EF Core + MySQL cannot reliably translate
            // AppointmentDate.Date + AppointmentTime into SQL across providers.
            var due = upcoming.Where(a =>
            {
                var start = a.AppointmentDate.Date + a.AppointmentTime;
                return start > now && start <= windowEnd;
            }).ToList();

            if (due.Count == 0) return;

            foreach (var appt in due)
            {
                var doctor = appt.Doctor;
                var patient = appt.Patient;
                if (doctor == null || patient == null)
                {
                    // Skip malformed rows; mark as sent so we don't loop forever.
                    appt.Reminder5MinSent = true;
                    appt.Reminder5MinAt = DateTime.UtcNow;
                    continue;
                }

                var subject = $"Your appointment with {patient.FullName} starts in 5 minutes";
                var when = $"{appt.AppointmentDate:dddd, d MMM yyyy} at {appt.AppointmentTime:hh\\:mm}";
                var body = $@"
                    <p>Hi Dr. {System.Net.WebUtility.HtmlEncode(doctor.FullName)},</p>
                    <p>This is a reminder that you have an upcoming appointment <b>in about 5 minutes</b>.</p>
                    <ul>
                        <li><b>Patient:</b> {System.Net.WebUtility.HtmlEncode(patient.FullName)}</li>
                        <li><b>When:</b> {System.Net.WebUtility.HtmlEncode(when)}</li>
                        <li><b>Reason:</b> {System.Net.WebUtility.HtmlEncode(appt.Reason)}</li>
                    </ul>
                    <p>Please be ready to receive the patient.</p>
                    <p>— Hospital Appointment System</p>";

                try
                {
                    await email.SendAsync(doctor.Email, subject, body);
                    appt.Reminder5MinSent = true;
                    appt.Reminder5MinAt = DateTime.UtcNow;
                    _log.LogInformation("5-minute reminder sent to {DoctorEmail} for appointment {AppointmentId}.",
                        doctor.Email, appt.Id);
                }
                catch (Exception ex)
                {
                    // Don't mark as sent — we'll retry on the next poll (within
                    // the 5-minute window). If the window closes, we simply miss it.
                    _log.LogWarning(ex, "Failed to send 5-minute reminder for appointment {AppointmentId}. Will retry.",
                        appt.Id);
                }
            }

            // Save any flags we changed (sent or skipped).
            await db.SaveChangesAsync(ct);
        }
    }
}