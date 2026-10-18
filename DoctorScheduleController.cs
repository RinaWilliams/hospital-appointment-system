using System.Security.Claims;
using HospitalApi.Data;
using HospitalApi.DTOs;
using HospitalApi.Models;
using HospitalApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/doctor-schedules")]
    public class DoctorScheduleController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly SlotService _slots;
        public DoctorScheduleController(AppDbContext db, SlotService slots) { _db = db; _slots = slots; }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Create(CreateScheduleRequest request)
        {
            if (request.DayOfWeek < 0 || request.DayOfWeek > 6 || request.StartTime >= request.EndTime)
                return BadRequest(new { message = "Provide a valid day of week and a start time before the end time." });

            var schedule = new DoctorSchedule
            {
                DoctorUserId = CurrentUserId,
                DayOfWeek = request.DayOfWeek,
                StartTime = request.StartTime,
                EndTime = request.EndTime
            };
            _db.DoctorSchedules.Add(schedule);
            await _db.SaveChangesAsync();
            return Ok(schedule);
        }

        [HttpGet("mine")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMine()
        {
            var schedules = await _db.DoctorSchedules.Where(s => s.DoctorUserId == CurrentUserId)
                .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime).ToListAsync();
            return Ok(schedules);
        }

        [HttpGet("{doctorUserId:int}")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> GetForDoctor(int doctorUserId)
        {
            var schedules = await _db.DoctorSchedules.Where(s => s.DoctorUserId == doctorUserId)
                .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime).ToListAsync();
            return Ok(schedules);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Delete(int id)
        {
            var schedule = await _db.DoctorSchedules.FindAsync(id);
            if (schedule == null || schedule.DoctorUserId != CurrentUserId) return NotFound();
            _db.DoctorSchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ---------- Time off (one-off blocked dates, separate from the weekly pattern above) ----------

        [HttpPost("time-off")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> CreateTimeOff(CreateTimeOffRequest request)
        {
            if (!DateTimeParsing.TryParseDate(request.Date, out var date))
                return BadRequest(new { message = "Invalid date format, expected yyyy-MM-dd." });

            TimeSpan? start = null, end = null;
            if (!string.IsNullOrWhiteSpace(request.StartTime) || !string.IsNullOrWhiteSpace(request.EndTime))
            {
                if (!DateTimeParsing.TryParseTime(request.StartTime ?? "", out var s) || !DateTimeParsing.TryParseTime(request.EndTime ?? "", out var e))
                    return BadRequest(new { message = "Provide both a start and end time, or leave both blank to block the whole day." });
                start = s; end = e;
            }

            var timeOff = new Models.DoctorTimeOff
            {
                DoctorUserId = CurrentUserId,
                Date = date.ToDateTime(TimeOnly.MinValue),
                StartTime = start,
                EndTime = end,
                Reason = request.Reason
            };
            _db.DoctorTimeOffs.Add(timeOff);
            await _db.SaveChangesAsync();
            return Ok(timeOff);
        }

        [HttpGet("time-off/mine")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMyTimeOff()
        {
            var items = await _db.DoctorTimeOffs.Where(t => t.DoctorUserId == CurrentUserId)
                .OrderBy(t => t.Date).ToListAsync();
            return Ok(items);
        }

        [HttpDelete("time-off/{id}")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> DeleteTimeOff(int id)
        {
            var timeOff = await _db.DoctorTimeOffs.FindAsync(id);
            if (timeOff == null || timeOff.DoctorUserId != CurrentUserId) return NotFound();
            _db.DoctorTimeOffs.Remove(timeOff);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        // Real-time available slot list for a given doctor + date, used by
        // the booking and reschedule forms. This is the conflict-detection
        // endpoint: it already excludes times taken by other appointments.
        [HttpGet("{doctorUserId:int}/slots")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSlots(int doctorUserId, [FromQuery] string date, [FromQuery] int? excludeAppointmentId = null)
        {
            if (!DateTimeParsing.TryParseDate(date, out var parsedDate))
                return BadRequest(new { message = "Invalid date format, expected yyyy-MM-dd." });

            var slots = await _slots.GetAvailableSlotsAsync(doctorUserId, parsedDate, excludeAppointmentId);
            return Ok(slots.Select(s => s.ToString(@"hh\:mm")));
        }
    }
}
