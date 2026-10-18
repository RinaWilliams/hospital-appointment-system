using System.Security.Claims;
using HospitalApi.Data;
using HospitalApi.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/doctor-availability")]
    [Authorize]
    public class DoctorAvailabilityController : ControllerBase
    {
        private readonly AppDbContext _db;
        public DoctorAvailabilityController(AppDbContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("me")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> GetMine()
        {
            var profile = await _db.DoctorProfiles.FindAsync(CurrentUserId);
            if (profile == null) return NotFound(new { message = "Doctor profile not found." });
            return Ok(new
            {
                isAcceptingBookings = profile.IsAcceptingBookings,
                note = profile.AvailabilityNote
            });
        }

        [HttpPut("me")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> SetMine(ToggleAvailabilityRequest request)
        {
            var profile = await _db.DoctorProfiles.FindAsync(CurrentUserId);
            if (profile == null) return NotFound(new { message = "Doctor profile not found." });
            profile.IsAcceptingBookings = request.IsAcceptingBookings;
            profile.AvailabilityNote = request.Note;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = request.IsAcceptingBookings
                    ? "You are now visible to patients. Bookings are open."
                    : "You are now hidden. Patients cannot book you.",
                isAcceptingBookings = profile.IsAcceptingBookings
            });
        }

        [HttpPut("{doctorUserId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetForDoctor(int doctorUserId, ToggleAvailabilityRequest request)
        {
            var profile = await _db.DoctorProfiles.FindAsync(doctorUserId);
            if (profile == null) return NotFound(new { message = "Doctor not found." });
            profile.IsAcceptingBookings = request.IsAcceptingBookings;
            profile.AvailabilityNote = request.Note;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Updated.", isAcceptingBookings = profile.IsAcceptingBookings });
        }
    }
}