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
    [Route("api/prescriptions")]
    [Authorize]
    public class PrescriptionsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PrescriptionsController(AppDbContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role)!;

        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Create(CreatePrescriptionRequest request)
        {
            var prescription = new Prescription
            {
                DoctorUserId = CurrentUserId,
                PatientUserId = request.PatientUserId,
                AppointmentId = request.AppointmentId,
                DrugId = request.DrugId,
                Medication = request.Medication,
                Dosage = request.Dosage,
                Instructions = request.Instructions,
                Quantity = request.Quantity,
                Status = PrescriptionStatus.Pending
            };
            _db.Prescriptions.Add(prescription);
            await _db.SaveChangesAsync();
            return Ok(prescription);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? patientUserId = null)
        {
            IQueryable<Prescription> query = _db.Prescriptions.Include(p => p.Doctor).Include(p => p.Patient);

            query = CurrentRole switch
            {
                "Patient" => query.Where(p => p.PatientUserId == CurrentUserId),
                "Doctor" => query.Where(p => p.DoctorUserId == CurrentUserId),
                "Pharmacist" => query,
                "Admin" => query,
                _ => query.Where(p => false)
            };
            if (patientUserId.HasValue) query = query.Where(p => p.PatientUserId == patientUserId);

            var results = await query.OrderByDescending(p => p.IssuedDate).Select(p => new
            {
                p.Id, p.Medication, p.Dosage, p.Instructions, p.Quantity,
                Status = p.Status.ToString(),
                Doctor = p.Doctor!.FullName, Patient = p.Patient!.FullName, PatientPhone = p.Patient.Phone,
                p.IssuedDate, p.DispensedDate
            }).ToListAsync();

            return Ok(results);
        }

        [HttpPut("{id}/dispense")]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> Dispense(int id)
        {
            var prescription = await _db.Prescriptions.FindAsync(id);
            if (prescription == null) return NotFound(new { message = "Prescription not found." });
            if (prescription.Status != PrescriptionStatus.Pending) return BadRequest(new { message = "This prescription has already been dispensed." });

            prescription.Status = PrescriptionStatus.Dispensed;
            prescription.DispensedByUserId = CurrentUserId;
            prescription.DispensedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Marked as dispensed." });
        }
    }
}
