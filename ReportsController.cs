using HospitalApi.Data;
using HospitalApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/reports")]
    [Authorize(Roles = "Admin")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ReportsController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            int total = await _db.Appointments.CountAsync();

            var statusCounts = Enum.GetValues<AppointmentStatus>().ToDictionary(s => s.ToString(), s => 0);
            var grouped = await _db.Appointments.GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key.ToString(), Count = g.Count() }).ToListAsync();
            foreach (var g in grouped) statusCounts[g.Status] = g.Count;

            decimal revenue = await _db.Payments.Where(p => p.Status == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Amount) ?? 0;
            int paidCount = await _db.Payments.CountAsync(p => p.Status == PaymentStatus.Paid);
            int failedCount = await _db.Payments.CountAsync(p => p.Status == PaymentStatus.Failed);

            var byDoctor = await _db.Users.Where(u => u.Role == UserRole.Doctor)
                .Include(u => u.DoctorProfile)
                .Select(d => new
                {
                    DoctorId = d.Id,
                    d.FullName,
                    Specialization = d.DoctorProfile != null ? d.DoctorProfile.Specialization : null,
                    Count = _db.Appointments.Count(a => a.DoctorUserId == d.Id)
                })
                .OrderByDescending(d => d.Count)
                .ToListAsync();

            return Ok(new
            {
                total,
                statusCounts,
                byDoctor,
                revenue,
                paidCount,
                failedCount
            });
        }
    }
}
