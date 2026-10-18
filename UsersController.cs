using HospitalApi.Data;
using HospitalApi.DTOs;
using HospitalApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Admin")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UsersController(AppDbContext db) => _db = db;

        // Strips an existing "Dr." / "Dr " prefix from a stored name.
        private static string StripDoctorTitle(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name ?? string.Empty;
            var s = name.Trim();
            if (s.StartsWith("Dr.", System.StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("Dr ", System.StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(3).Trim();
            }
            return s;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _db.Users.Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                Role = u.Role.ToString(),
                u.Phone,
                u.IsActive,
                u.CreatedAt
            }).ToListAsync();
            return Ok(users);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserRequest request)
        {
            if (request.Role == UserRole.Patient)
                return BadRequest(new { message = "Patients self-register at /api/auth/register." });

            if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 3)
                return BadRequest(new { message = "Full name must be at least 3 characters." });

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required." });

            if (await _db.Users.AnyAsync(u => u.Email == request.Email))
                return Conflict(new { message = "An account with that email already exists." });

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
                return BadRequest(new { message = "Password must be at least 6 characters." });

            if (!string.IsNullOrWhiteSpace(request.Phone) &&
                !System.Text.RegularExpressions.Regex.IsMatch(request.Phone, @"^\d{11}$"))
                return BadRequest(new { message = "Phone number must be exactly 11 digits, numbers only." });

            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11),
                Role = request.Role,
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone,
                IsActive = true,
                EmailConfirmed = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            if (request.Role == UserRole.Doctor)
            {
                _db.DoctorProfiles.Add(new DoctorProfile
                {
                    UserId = user.Id,
                    Specialization = string.IsNullOrWhiteSpace(request.Specialization) ? "General Medicine" : request.Specialization!.Trim(),
                    Bio = request.Bio,
                    ConsultationFee = request.ConsultationFee ?? 5000.00m,
                    SlotDurationMinutes = request.SlotDurationMinutes ?? 30,
                    FacilityId = request.FacilityId,
                    IsAcceptingBookings = false
                });
                await _db.SaveChangesAsync();
            }

            return Ok(new { message = "Account created.", userId = user.Id });
        }

        [HttpPut("{id}/toggle")]
        public async Task<IActionResult> Toggle(int id, ToggleActiveRequest request)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsActive = request.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Updated." });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "User deactivated." });
        }

        // ===== Public doctor directory — returns name WITHOUT "Dr." prefix =====
        [HttpGet("/api/doctors")]
        [AllowAnonymous]
        public async Task<IActionResult> ListDoctors(
            [FromQuery] string? specialization,
            [FromQuery] int? facilityId,
            [FromQuery] bool includeUnavailable = false)
        {
            var query = _db.Users.Where(u => u.Role == UserRole.Doctor && u.IsActive)
                .Include(u => u.DoctorProfile).ThenInclude(p => p!.Facility)
                .AsQueryable();

            if (!includeUnavailable)
                query = query.Where(u => u.DoctorProfile!.IsAcceptingBookings);

            if (!string.IsNullOrWhiteSpace(specialization))
                query = query.Where(u => u.DoctorProfile!.Specialization == specialization);
            if (facilityId.HasValue)
                query = query.Where(u => u.DoctorProfile!.FacilityId == facilityId);

            var raw = await query.Select(u => new
            {
                u.Id,
                u.FullName,
                Specialization = u.DoctorProfile!.Specialization,
                Bio = u.DoctorProfile.Bio,
                ConsultationFee = u.DoctorProfile.ConsultationFee,
                SlotDurationMinutes = u.DoctorProfile.SlotDurationMinutes,
                Facility = u.DoctorProfile.Facility != null ? u.DoctorProfile.Facility.Name : null,
                FacilityId = u.DoctorProfile.FacilityId,
                IsAcceptingBookings = u.DoctorProfile.IsAcceptingBookings,
                AvailabilityNote = u.DoctorProfile.AvailabilityNote
            }).ToListAsync();

            // Apply the "Dr." strip in memory (EF can't translate our helper)
            var doctors = raw.Select(d => new
            {
                d.Id,
                FullName = StripDoctorTitle(d.FullName),
                d.Specialization,
                d.Bio,
                d.ConsultationFee,
                d.SlotDurationMinutes,
                d.Facility,
                d.FacilityId,
                d.IsAcceptingBookings,
                d.AvailabilityNote
            }).ToList();

            return Ok(doctors);
        }

        // ===== Doctor list for complaint dropdown — also stripped =====
        [HttpGet("/api/doctors/all")]
        [Authorize]
        public async Task<IActionResult> ListAllDoctors()
        {
            var raw = await _db.Users
                .Where(u => u.Role == UserRole.Doctor)
                .Include(u => u.DoctorProfile).ThenInclude(p => p!.Facility)
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.IsActive,
                    Specialization = u.DoctorProfile != null ? u.DoctorProfile.Specialization : null,
                    Facility = u.DoctorProfile != null && u.DoctorProfile.Facility != null ? u.DoctorProfile.Facility.Name : null,
                    IsAcceptingBookings = u.DoctorProfile != null && u.DoctorProfile.IsAcceptingBookings
                })
                .ToListAsync();

            var doctors = raw.Select(d => new
            {
                d.Id,
                FullName = StripDoctorTitle(d.FullName),
                d.IsActive,
                d.Specialization,
                d.Facility,
                d.IsAcceptingBookings
            }).ToList();

            return Ok(doctors);
        }
    }
}