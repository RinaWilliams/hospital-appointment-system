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
    [Route("api/complaints")]
    [Authorize]
    public class ComplaintsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ComplaintsController(AppDbContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role)!;

        [HttpPost]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Create(CreateComplaintRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Subject))
                return BadRequest(new { message = "Please enter a subject for your complaint." });
            if (request.Subject.Trim().Length < 5)
                return BadRequest(new { message = "Subject must be at least 5 characters." });
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { message = "Please describe the issue in the message box." });
            if (request.Message.Trim().Length < 15)
                return BadRequest(new { message = "Please give a bit more detail (at least 15 characters)." });

            // If a doctor is provided, verify they exist and are a doctor.
            if (request.DoctorUserId.HasValue)
            {
                var exists = await _db.Users.AnyAsync(u => u.Id == request.DoctorUserId.Value && u.Role == UserRole.Doctor);
                if (!exists) return BadRequest(new { message = "The selected doctor could not be found." });
            }

            var complaint = new Complaint
            {
                PatientUserId = CurrentUserId,
                DoctorUserId = request.DoctorUserId,
                Subject = request.Subject.Trim(),
                Message = request.Message.Trim(),
                Status = ComplaintStatus.Open
            };
            _db.Complaints.Add(complaint);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Complaint submitted.", id = complaint.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            IQueryable<Complaint> query = _db.Complaints
                .Include(c => c.Patient)
                .Include(c => c.Doctor);

            query = CurrentRole switch
            {
                "Patient" => query.Where(c => c.PatientUserId == CurrentUserId),
                "Admin" => query,
                _ => query.Where(c => false)
            };

            var results = await query.OrderByDescending(c => c.CreatedAt).Select(c => new
            {
                c.Id,
                c.Subject,
                c.Message,
                Status = c.Status.ToString(),
                c.AdminResponse,
                Patient = c.Patient!.FullName,
                DoctorUserId = c.DoctorUserId,
                Doctor = c.Doctor != null ? c.Doctor.FullName : null,
                c.CreatedAt
            }).ToListAsync();

            return Ok(results);
        }

        [HttpPut("{id}/respond")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Respond(int id, RespondComplaintRequest request)
        {
            if (!Enum.TryParse<ComplaintStatus>(request.Status, out var status))
                return BadRequest(new { message = "A valid status is required." });

            var complaint = await _db.Complaints.FindAsync(id);
            if (complaint == null) return NotFound(new { message = "Complaint not found." });

            complaint.Status = status;
            complaint.AdminResponse = request.AdminResponse;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Complaint updated." });
        }
    }
}