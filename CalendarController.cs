using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HospitalApi.Data;
using HospitalApi.Models;
using HospitalApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/calendar")]
    public class CalendarController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        public CalendarController(AppDbContext db, IConfiguration config) { _db = db; _config = config; }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private static string NewToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        // Item x: base URL for the feed.
        // Priority: 1) Calendar:PublicBaseUrl 2) forwarded headers 3) request host.
        private string BaseUrl()
        {
            var configured = _config["Calendar:PublicBaseUrl"];
            if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');

            var proto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
            var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(host))
                return $"{(string.IsNullOrWhiteSpace(proto) ? Request.Scheme : proto)}://{host}";

            return $"{Request.Scheme}://{Request.Host}";
        }

        private object LinkInfo(string token)
        {
            var baseUrl = BaseUrl();
            var httpUrl = $"{baseUrl}/api/calendar/feed/{token}.ics";
            var webcal = "webcal://" + httpUrl.Substring(httpUrl.IndexOf("://", StringComparison.Ordinal) + 3);

            string host;
            try { host = new Uri(httpUrl).Host; } catch { host = ""; }
            bool localOnly = host == "localhost" || host == "127.0.0.1" || host == "::1";

            // Item x: helpful hint when the URL points at localhost.
            string? hint = null;
            if (localOnly)
            {
                hint = "This URL points to your computer, so a phone cannot reach it. To make reminders " +
                       "work on a real phone: (1) set \"Calendar:PublicBaseUrl\" in appsettings.json to " +
                       "your computer's Wi-Fi IP (e.g. http://192.168.1.42:62234), or expose the API " +
                       "with a public tunnel (ngrok). Then press \"New link\" and subscribe again.";
            }

            return new { url = httpUrl, webcalUrl = webcal, localOnly, hint };
        }

        [HttpGet("link")]
        [Authorize(Roles = "Patient,Doctor")]
        public async Task<IActionResult> GetLink()
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            if (string.IsNullOrEmpty(user.CalendarToken))
            {
                user.CalendarToken = NewToken();
                await _db.SaveChangesAsync();
            }
            return Ok(LinkInfo(user.CalendarToken));
        }

        [HttpPost("regenerate")]
        [Authorize(Roles = "Patient,Doctor")]
        public async Task<IActionResult> Regenerate()
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            user.CalendarToken = NewToken();
            await _db.SaveChangesAsync();
            return Ok(LinkInfo(user.CalendarToken));
        }

        [HttpGet("mine.ics")]
        [Authorize(Roles = "Patient,Doctor")]
        public async Task<IActionResult> DownloadMine()
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user == null) return NotFound();
            var ics = await BuildFeed(user);
            return File(Encoding.UTF8.GetBytes(ics), "text/calendar", "my-appointments.ics");
        }

        [HttpGet("feed/{token}.ics")]
        [AllowAnonymous]
        public async Task<IActionResult> Feed(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 64) return NotFound();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.CalendarToken == token && u.IsActive);
            if (user == null) return NotFound();

            var ics = await BuildFeed(user);
            Response.Headers["Cache-Control"] = "no-cache";
            return Content(ics, "text/calendar; charset=utf-8");
        }

        private async Task<string> BuildFeed(User user)
        {
            bool isDoctor = user.Role == UserRole.Doctor;
            var from = DateTime.Today.AddDays(-1);

            var query = _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor).ThenInclude(d => d!.DoctorProfile).ThenInclude(p => p!.Facility)
                .Where(a => a.AppointmentDate >= from
                    && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Approved));

            query = isDoctor
                ? query.Where(a => a.DoctorUserId == user.Id)
                : query.Where(a => a.PatientUserId == user.Id);

            var list = await query.OrderBy(a => a.AppointmentDate).ThenBy(a => a.AppointmentTime).ToListAsync();

            var sb = IcsBuilder.BeginCalendar(isDoctor ? "My Patient Appointments" : "My Hospital Appointments", true);
            foreach (var a in list) IcsBuilder.AppendEvent(sb, a, forPatient: !isDoctor);
            return IcsBuilder.EndCalendar(sb);
        }
    }
}