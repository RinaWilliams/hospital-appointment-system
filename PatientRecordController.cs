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
    [Route("api/patientrecords")]
    [Authorize]
    public class PatientRecordController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PatientRecordController(AppDbContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role)!;

        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Create(CreatePatientRecordRequest request)
        {
            var record = new PatientRecord
            {
                DoctorUserId = CurrentUserId,
                PatientUserId = request.PatientUserId,
                Notes = request.Notes
            };
            _db.PatientRecords.Add(record);
            await _db.SaveChangesAsync();
            return Ok(record);
        }

        // Patients see their own history; doctors and admins see everything they're allowed to
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? patientUserId = null)
        {
            IQueryable<PatientRecord> query = _db.PatientRecords.Include(r => r.Doctor).Include(r => r.Patient);

            query = CurrentRole switch
            {
                "Patient" => query.Where(r => r.PatientUserId == CurrentUserId),
                "Doctor" => query.Where(r => r.DoctorUserId == CurrentUserId),
                "Admin" => query,
                _ => query.Where(r => false)
            };
            if (patientUserId.HasValue) query = query.Where(r => r.PatientUserId == patientUserId);

            var results = await query.OrderByDescending(r => r.RecordDate).Select(r => new
            {
                r.Id, r.Notes, r.RecordDate,
                Doctor = r.Doctor!.FullName, Patient = r.Patient!.FullName
            }).ToListAsync();

            return Ok(results);
        }
    }
}
