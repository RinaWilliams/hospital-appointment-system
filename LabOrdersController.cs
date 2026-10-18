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
    [Route("api/laborders")]
    [Authorize]
    public class LabOrdersController : ControllerBase
    {
        private readonly AppDbContext _db;
        public LabOrdersController(AppDbContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role)!;

        [HttpPost]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Create(CreateLabOrderRequest request)
        {
            var order = new LabOrder
            {
                DoctorUserId = CurrentUserId,
                PatientUserId = request.PatientUserId,
                TestName = request.TestName,
                Notes = request.Notes,
                Status = LabOrderStatus.Ordered
            };
            _db.LabOrders.Add(order);
            await _db.SaveChangesAsync();
            return Ok(order);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? patientUserId = null)
        {
            IQueryable<LabOrder> query = _db.LabOrders
                .Include(t => t.Doctor).Include(t => t.Patient).Include(t => t.CompletedBy);

            query = CurrentRole switch
            {
                "Patient" => query.Where(t => t.PatientUserId == CurrentUserId),
                "Doctor" => query.Where(t => t.DoctorUserId == CurrentUserId),
                "LabTechnician" => query,
                "Admin" => query,
                _ => query.Where(t => false)
            };
            if (patientUserId.HasValue) query = query.Where(t => t.PatientUserId == patientUserId);

            var results = await query.OrderByDescending(t => t.RequestedDate).Select(t => new
            {
                t.Id, t.TestName, t.Notes,
                Status = t.Status.ToString(),
                Doctor = t.Doctor!.FullName,
                Patient = t.Patient!.FullName,
                PatientPhone = t.Patient.Phone,
                t.RequestedDate,
                ResultText = t.ResultText,
                CompletedByName = t.CompletedBy != null ? t.CompletedBy.FullName : null,
                t.CompletedAt
            }).ToListAsync();

            return Ok(results);
        }

        [HttpPost("{id}/complete")]
        [Authorize(Roles = "LabTechnician")]
        public async Task<IActionResult> Complete(int id, SubmitLabResultRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ResultText)) return BadRequest(new { message = "A result is required." });

            var order = await _db.LabOrders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Lab order not found." });
            if (order.Status != LabOrderStatus.Ordered) return BadRequest(new { message = "This order has already been completed." });

            order.Status = LabOrderStatus.Completed;
            order.ResultText = request.ResultText;
            order.CompletedByUserId = CurrentUserId;
            order.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Result recorded." });
        }
    }
}
