using System.Security.Claims;
using HospitalApi.Data;
using HospitalApi.DTOs;
using HospitalApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ProfileController(AppDbContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var user = await _db.Users.Include(u => u.PatientProfile).Include(u => u.DoctorProfile)
                .FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Phone,
                Role = user.Role.ToString(),
                MedicalHistory = user.PatientProfile?.MedicalHistory,
                Specialization = user.DoctorProfile?.Specialization,
                Bio = user.DoctorProfile?.Bio,
                ConsultationFee = user.DoctorProfile?.ConsultationFee,
                SlotDurationMinutes = user.DoctorProfile?.SlotDurationMinutes,
                FacilityId = user.DoctorProfile?.FacilityId
            });
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
        {
            var user = await _db.Users.Include(u => u.PatientProfile).Include(u => u.DoctorProfile)
                .FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            if (user == null) return NotFound();

            user.FullName = request.FullName;
            user.Phone = request.Phone;

            if (user.Role == UserRole.Patient && user.PatientProfile != null)
            {
                user.PatientProfile.MedicalHistory = request.MedicalHistory;
            }
            else if (user.Role == UserRole.Doctor && user.DoctorProfile != null)
            {
                user.DoctorProfile.Bio = request.Bio;
                if (request.ConsultationFee.HasValue) user.DoctorProfile.ConsultationFee = request.ConsultationFee.Value;
                if (request.SlotDurationMinutes.HasValue) user.DoctorProfile.SlotDurationMinutes = Math.Max(5, request.SlotDurationMinutes.Value);
                user.DoctorProfile.FacilityId = request.FacilityId;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Profile updated." });
        }
    }
}
