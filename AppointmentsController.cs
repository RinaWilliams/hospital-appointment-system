using System.Security.Claims;
using System.Text;
using HospitalApi.Data;
using HospitalApi.DTOs;
using HospitalApi.Models;
using HospitalApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static HospitalApi.Services.DateTimeParsing;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/appointments")]
    [Authorize]
    public class AppointmentsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly SlotService _slots;
        public AppointmentsController(AppDbContext db, SlotService slots) { _db = db; _slots = slots; }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        private string CurrentRole => User.FindFirstValue(ClaimTypes.Role)!;

        [HttpPost]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Create(CreateAppointmentRequest request)
        {
            var doctor = await _db.Users.Include(u => u.DoctorProfile)
                .FirstOrDefaultAsync(u => u.Id == request.DoctorUserId && u.Role == UserRole.Doctor && u.IsActive);
            if (doctor?.DoctorProfile == null) return NotFound(new { message = "That doctor could not be found." });
            if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { message = "A reason for the visit is required." });
            if (!DateTimeParsing.TryParseDate(request.Date, out var date) || !DateTimeParsing.TryParseTime(request.Time, out var time))
                return BadRequest(new { message = "Invalid date or time format." });

            // Strict past-time rejection (server-side)
            var chosenDateTime = date.ToDateTime(TimeOnly.MinValue) + time;
            if (chosenDateTime <= DateTime.Now)
                return BadRequest(new { message = "That time has already passed — please pick a later slot." });

            var openSlots = await _slots.GetAvailableSlotsAsync(request.DoctorUserId, date);
            if (!openSlots.Contains(time))
                return Conflict(new { message = "That slot is no longer available - please pick another time." });

            var appt = new Appointment
            {
                PatientUserId = CurrentUserId,
                DoctorUserId = request.DoctorUserId,
                AppointmentDate = date.ToDateTime(TimeOnly.MinValue),
                AppointmentTime = time,
                Reason = request.Reason,
                FeeAmount = doctor.DoctorProfile.ConsultationFee,
                Status = AppointmentStatus.PendingPayment
            };
            _db.Appointments.Add(appt);
            await _db.SaveChangesAsync();

            var payment = new Payment
            {
                AppointmentId = appt.Id,
                PatientUserId = CurrentUserId,
                Amount = appt.FeeAmount,
                Reference = "PAY-" + Guid.NewGuid().ToString("N")[..10].ToUpper(),
                Status = PaymentStatus.Pending
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Appointment created - payment required to confirm the slot.", appointmentId = appt.Id, feeAmount = appt.FeeAmount, paymentId = payment.Id });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? doctorId = null)
        {
            IQueryable<Appointment> query = _db.Appointments
                .Include(a => a.Patient).Include(a => a.Doctor).ThenInclude(d => d!.DoctorProfile)
                .Include(a => a.Payment);

            query = CurrentRole switch
            {
                "Patient" => query.Where(a => a.PatientUserId == CurrentUserId),
                "Doctor" => query.Where(a => a.DoctorUserId == CurrentUserId && a.Status != AppointmentStatus.PendingPayment),
                "Admin" => doctorId.HasValue ? query.Where(a => a.DoctorUserId == doctorId) : query,
                _ => query.Where(a => false)
            };

            var results = await query.OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.AppointmentTime)
                .Select(a => new
                {
                    a.Id,
                    a.AppointmentDate,
                    AppointmentTime = a.AppointmentTime.ToString(@"hh\:mm"),
                    a.Reason,
                    a.DoctorNote,
                    a.FeeAmount,
                    Status = a.Status.ToString(),
                    Patient = a.Patient!.FullName,
                    PatientPhone = a.Patient.Phone,
                    PatientUserId = a.PatientUserId,
                    Doctor = a.Doctor!.FullName,
                    Specialization = a.Doctor.DoctorProfile != null ? a.Doctor.DoctorProfile.Specialization : null,
                    DoctorUserId = a.DoctorUserId,
                    PaymentReference = a.Payment != null && a.Payment.Status == PaymentStatus.Paid ? a.Payment.Reference : null
                }).ToListAsync();

            return Ok(results);
        }

        [HttpPut("{id}/respond")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Respond(int id, RespondAppointmentRequest request)
        {
            if (request.Status != "Approved" && request.Status != "Rejected")
                return BadRequest(new { message = "Status must be 'Approved' or 'Rejected'." });

            var appt = await _db.Appointments.FindAsync(id);
            if (appt == null) return NotFound(new { message = "Appointment not found." });
            if (appt.DoctorUserId != CurrentUserId) return Forbid();
            if (appt.Status != AppointmentStatus.Pending)
                return BadRequest(new { message = $"This appointment has already been {appt.Status}." });

            appt.Status = Enum.Parse<AppointmentStatus>(request.Status);
            appt.DoctorNote = request.DoctorNote;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Updated." });
        }

        [HttpPut("{id}/complete")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Complete(int id)
        {
            var appt = await _db.Appointments.FindAsync(id);
            if (appt == null) return NotFound(new { message = "Appointment not found." });
            if (appt.DoctorUserId != CurrentUserId) return Forbid();
            if (appt.Status != AppointmentStatus.Approved)
                return BadRequest(new { message = "Only approved appointments can be marked completed." });

            appt.Status = AppointmentStatus.Completed;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Marked as completed." });
        }

        [HttpPut("{id}/cancel")]
        [Authorize(Roles = "Patient,Admin")]
        public async Task<IActionResult> Cancel(int id)
        {
            var appt = await _db.Appointments.FindAsync(id);
            if (appt == null) return NotFound(new { message = "Appointment not found." });

            if (CurrentRole == "Patient")
            {
                if (appt.PatientUserId != CurrentUserId) return Forbid();
                if (appt.Status is not (AppointmentStatus.PendingPayment or AppointmentStatus.Pending or AppointmentStatus.Approved))
                    return BadRequest(new { message = "Only unconfirmed or upcoming appointments can be cancelled." });
                if (appt.Status != AppointmentStatus.PendingPayment && !SlotService.IsBeyondChangeCutoff(appt.AppointmentDate, appt.AppointmentTime))
                    return BadRequest(new { message = "Too close to the appointment time to cancel - please contact the clinic directly." });
            }
            else
            {
                if (appt.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.Rejected)
                    return BadRequest(new { message = $"This appointment is already {appt.Status}." });
            }

            appt.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Cancelled." });
        }

        [HttpPut("{id}/reschedule")]
        [Authorize(Roles = "Patient,Admin")]
        public async Task<IActionResult> Reschedule(int id, RescheduleAppointmentRequest request)
        {
            var appt = await _db.Appointments.FindAsync(id);
            if (appt == null) return NotFound(new { message = "Appointment not found." });

            if (CurrentRole == "Patient")
            {
                if (appt.PatientUserId != CurrentUserId) return Forbid();
                if (appt.Status is not (AppointmentStatus.Pending or AppointmentStatus.Approved))
                    return BadRequest(new { message = "Only pending or approved appointments can be rescheduled." });
                if (!SlotService.IsBeyondChangeCutoff(appt.AppointmentDate, appt.AppointmentTime))
                    return BadRequest(new { message = "Too close to the appointment time to reschedule - please contact the clinic directly." });
            }
            else
            {
                if (appt.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.Rejected)
                    return BadRequest(new { message = $"Can't reschedule a {appt.Status} appointment." });
            }

            if (!DateTimeParsing.TryParseDate(request.Date, out var newDate) || !DateTimeParsing.TryParseTime(request.Time, out var newTime))
                return BadRequest(new { message = "Invalid date or time format." });

            // Strict past-time check for reschedule
            var chosenDateTime = newDate.ToDateTime(TimeOnly.MinValue) + newTime;
            if (chosenDateTime <= DateTime.Now)
                return BadRequest(new { message = "That time has already passed — please pick a later slot." });

            var openSlots = await _slots.GetAvailableSlotsAsync(appt.DoctorUserId, newDate, excludeAppointmentId: id);
            if (!openSlots.Contains(newTime))
                return Conflict(new { message = "That slot is not available - please pick another time." });

            if (appt.Status == AppointmentStatus.Approved) appt.Status = AppointmentStatus.Pending;

            appt.AppointmentDate = newDate.ToDateTime(TimeOnly.MinValue);
            appt.AppointmentTime = newTime;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Appointment rescheduled." });
        }

        [HttpGet("{id}/ics")]
        [Authorize(Roles = "Patient,Doctor,Admin")]
        public async Task<IActionResult> GetIcs(int id)
        {
            var appt = await _db.Appointments.Include(a => a.Patient).Include(a => a.Doctor)
                .ThenInclude(d => d!.DoctorProfile).ThenInclude(p => p!.Facility).FirstOrDefaultAsync(a => a.Id == id);
            if (appt == null) return NotFound();

            bool isOwner = (CurrentRole == "Patient" && appt.PatientUserId == CurrentUserId)
                || (CurrentRole == "Doctor" && appt.DoctorUserId == CurrentUserId)
                || CurrentRole == "Admin";
            if (!isOwner) return Forbid();

            var ics = IcsBuilder.SingleAppointment(appt, forPatient: CurrentRole == "Patient");
            return File(Encoding.UTF8.GetBytes(ics), "text/calendar", $"appointment-{appt.Id}.ics");
        }
    }
}