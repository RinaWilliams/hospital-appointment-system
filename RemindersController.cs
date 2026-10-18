using System.Security.Claims;
using HospitalApi.Data;
using HospitalApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    /// <summary>
    /// Demo/testing helper for the 5-minute reminder. In a real deployment this
    /// endpoint would not exist — reminders run automatically via the hosted
    /// AppointmentReminderService. Here, a doctor can force a reminder to fire
    /// immediately for their next approved appointment, so the feature is
    /// easy to demonstrate without waiting five real minutes.
    /// </summary>
    [ApiController]
    [Route("api/reminders")]
    [Authorize(Roles = "Doctor")]
    public class RemindersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly Services.EmailService _email;
        private readonly ILogger<RemindersController> _log;

        public RemindersController(AppDbContext db, Services.EmailService email, ILogger<RemindersController> log)
        {
            _db = db; _email = email; _log = log;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("test-next")]
        public async Task<IActionResult> TestNext()
        {
            // Find the doctor's next approved, not-yet-reminded appointment.
            var candidates = await _db.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .Where(a => a.DoctorUserId == CurrentUserId
                            && a.Status == AppointmentStatus.Approved
                            && !a.Reminder5MinSent)
                .ToListAsync();

            var now = DateTime.Now;
            var next = candidates
                .Where(a => (a.AppointmentDate.Date + a.AppointmentTime) > now)
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.AppointmentTime)
                .FirstOrDefault();

            if (next == null)
                return NotFound(new { message = "You have no upcoming approved appointments to remind you about." });

            var when = $"{next.AppointmentDate:dddd, d MMM yyyy} at {next.AppointmentTime:hh\\:mm}";
            var body = $@"
                <p>Hi Dr. {System.Net.WebUtility.HtmlEncode(next.Doctor!.FullName)},</p>
                <p><b>[TEST MODE]</b> This is what your 5-minute reminder looks like.</p>
                <ul>
                    <li><b>Patient:</b> {System.Net.WebUtility.HtmlEncode(next.Patient!.FullName)}</li>
                    <li><b>When:</b> {System.Net.WebUtility.HtmlEncode(when)}</li>
                    <li><b>Reason:</b> {System.Net.WebUtility.HtmlEncode(next.Reason)}</li>
                </ul>
                <p>— Hospital Appointment System</p>";

            await _email.SendAsync(next.Doctor.Email, "[TEST] 5-minute reminder preview", body);

            // Mark as sent so the real worker won't also fire for the same one.
            next.Reminder5MinSent = true;
            next.Reminder5MinAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _log.LogInformation("Test reminder sent to {Email} for appointment {Id}.", next.Doctor.Email, next.Id);

            return Ok(new
            {
                message = "Test reminder sent.",
                appointmentId = next.Id,
                sentTo = next.Doctor.Email,
                patient = next.Patient.FullName,
                when
            });
        }
    }
}