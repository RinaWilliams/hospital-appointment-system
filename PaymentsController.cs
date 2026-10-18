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
    [Route("api/payments")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PaymentsController(AppDbContext db) => _db = db;

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role)!;

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            IQueryable<Payment> query = _db.Payments.Include(p => p.Patient).Include(p => p.Appointment);
            query = CurrentRole switch
            {
                "Patient" => query.Where(p => p.PatientUserId == CurrentUserId),
                "Admin" => query,
                _ => query.Where(p => false)
            };

            var results = await query.OrderByDescending(p => p.PaymentDate).Select(p => new
            {
                p.Id, p.Amount, p.Reference, Status = p.Status.ToString(), p.PaymentDate,
                Patient = p.Patient!.FullName, AppointmentId = p.AppointmentId
            }).ToListAsync();

            return Ok(results);
        }

        
        [HttpPut("{id}/confirm")]
        [Authorize(Roles = "Patient,Admin")]
        public async Task<IActionResult> Confirm(int id, ConfirmPaymentRequest request)
        {
            var payment = await _db.Payments.Include(p => p.Appointment).FirstOrDefaultAsync(p => p.Id == id);
            if (payment == null) return NotFound(new { message = "Payment not found." });
            if (CurrentRole == "Patient" && payment.PatientUserId != CurrentUserId) return Forbid();
            if (payment.Status != PaymentStatus.Pending) return BadRequest(new { message = "This payment has already been processed." });

            if (request.Succeed)
            {
                payment.Status = PaymentStatus.Paid;
                if (payment.Appointment != null && payment.Appointment.Status == AppointmentStatus.PendingPayment)
                    payment.Appointment.Status = AppointmentStatus.Pending;
            }
            else
            {
                payment.Status = PaymentStatus.Failed;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = request.Succeed ? "Payment confirmed." : "Payment failed.", status = payment.Status.ToString() });
        }
    }
}
