using System.Security.Cryptography;
using HospitalApi.Data;
using HospitalApi.DTOs;
using HospitalApi.Models;
using HospitalApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TokenService _tokenService;
        private readonly EmailService _email;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext db, TokenService tokenService, EmailService email, IConfiguration config)
        {
            _db = db; _tokenService = tokenService; _email = email; _config = config;
        }

        private static string NewToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                   .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private string FrontendBase() =>
            (_config["App:FrontendBaseUrl"] ?? "http://localhost:5500").TrimEnd('/');

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 3)
                return BadRequest(new { message = "Please enter your full name (at least 3 characters)." });

            if (await _db.Users.AnyAsync(u => u.Email == request.Email))
                return Conflict(new { message = "An account with that email already exists." });

            if (!string.IsNullOrWhiteSpace(request.Phone) && !System.Text.RegularExpressions.Regex.IsMatch(request.Phone, @"^\d{11}$"))
                return BadRequest(new { message = "Phone number must be exactly 11 digits, numbers only." });

            bool isMinor = false;
            if (request.DateOfBirth.HasValue)
            {
                var age = DateTime.Today.Year - request.DateOfBirth.Value.Year;
                if (request.DateOfBirth.Value.Date > DateTime.Today.AddYears(-age)) age--;
                isMinor = age >= 0 && age < 18;
            }

            if (isMinor)
            {
                if (string.IsNullOrWhiteSpace(request.GuardianName))
                    return BadRequest(new { message = "Guardian name is required for under-18 registration." });
                if (string.IsNullOrWhiteSpace(request.GuardianPhone) || !System.Text.RegularExpressions.Regex.IsMatch(request.GuardianPhone, @"^\d{11}$"))
                    return BadRequest(new { message = "Guardian phone number must be exactly 11 digits." });
                if (string.IsNullOrWhiteSpace(request.GuardianEmail))
                    return BadRequest(new { message = "Guardian email is required for under-18 registration." });
                if (string.IsNullOrWhiteSpace(request.GuardianRelationship))
                    return BadRequest(new { message = "Please select your relationship to the guardian." });
                if (string.IsNullOrWhiteSpace(request.NIN) || !System.Text.RegularExpressions.Regex.IsMatch(request.NIN, @"^\d{11}$"))
                    return BadRequest(new { message = "A valid 11-digit NIN is required for under-18 registration." });
                if (!request.NINVerified)
                    return BadRequest(new { message = "NIN must be verified before registration." });
                if (!request.GuardianConsent)
                    return BadRequest(new { message = "Guardian consent is required." });
            }

            Gender? gender = null;
            if (!string.IsNullOrWhiteSpace(request.Gender))
            {
                if (!Enum.TryParse<Gender>(request.Gender, ignoreCase: true, out var g))
                    return BadRequest(new { message = "Gender must be Male, Female, or Other." });
                gender = g;
            }

            var confirmToken = NewToken();
            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11),
                Role = UserRole.Patient,
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone,
                IsActive = true,
                EmailConfirmed = false,
                EmailConfirmToken = confirmToken,
                EmailConfirmSentAt = DateTime.UtcNow
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _db.PatientProfiles.Add(new PatientProfile
            {
                UserId = user.Id,
                DateOfBirth = request.DateOfBirth,
                Gender = gender,
                Address = request.Address,
                IsMinor = isMinor,
                GuardianName = request.GuardianName,
                GuardianPhone = request.GuardianPhone,
                GuardianEmail = request.GuardianEmail,
                GuardianRelationship = request.GuardianRelationship,
                GuardianConsent = isMinor ? request.GuardianConsent : (bool?)null,
                NIN = isMinor ? request.NIN : null,
                NINVerified = isMinor && request.NINVerified,
                NINVerifiedAt = isMinor && request.NINVerified ? DateTime.UtcNow : (DateTime?)null
            });
            await _db.SaveChangesAsync();

            var confirmUrl = $"{FrontendBase()}/pages/confirm-email.html?token={confirmToken}";
            await _email.SendAsync(user.Email,
                "Confirm your Hospital Appointment System account",
                $@"<p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
                   <p>Welcome to the Hospital Appointment System. Please confirm your email address by clicking the link below:</p>
                   <p><a href=""{confirmUrl}"">Confirm my email</a></p>
                   <p>If you didn't create this account, you can ignore this message.</p>");

            if (isMinor && !string.IsNullOrWhiteSpace(request.GuardianEmail))
            {
                await _email.SendAsync(request.GuardianEmail!,
                    "Your ward has registered on the Hospital Appointment System",
                    $@"<p>Hello,</p>
                       <p><b>{System.Net.WebUtility.HtmlEncode(user.FullName)}</b> has registered as a patient and listed you as their guardian.</p>
                       <p>Their NIN has been verified. If you do not consent to this registration, please contact the clinic immediately.</p>");
            }

            var token = _tokenService.GenerateToken(user);
            return Ok(new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                FullName = user.FullName,
                Role = user.Role.ToString(),
                EmailConfirmed = user.EmailConfirmed
            });
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid email or password." });
            if (!user.IsActive)
                return Unauthorized(new { message = "This account has been deactivated. Contact an administrator." });

            var token = _tokenService.GenerateToken(user);
            return Ok(new AuthResponse
            {
                Token = token,
                UserId = user.Id,
                FullName = user.FullName,
                Role = user.Role.ToString(),
                EmailConfirmed = user.EmailConfirmed
            });
        }

        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(new { message = "Missing confirmation token." });
            var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailConfirmToken == request.Token);
            if (user == null) return BadRequest(new { message = "This confirmation link is invalid or has already been used." });
            user.EmailConfirmed = true;
            user.EmailConfirmToken = null;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Email confirmed. You can now sign in." });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user != null)
            {
                user.PasswordResetToken = NewToken();
                user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);
                await _db.SaveChangesAsync();

                var resetUrl = $"{FrontendBase()}/pages/reset-password.html?token={user.PasswordResetToken}";
                await _email.SendAsync(user.Email,
                    "Reset your Hospital Appointment System password",
                    $@"<p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
                       <p>We received a request to reset your password. Click the link below to choose a new one.</p>
                       <p><a href=""{resetUrl}"">Reset my password</a></p>
                       <p>This link expires in 1 hour. If you didn't ask for it, ignore this email.</p>");
            }
            return Ok(new { message = "If an account exists for that email, a reset link has been sent." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(new { message = "Missing reset token." });
            var user = await _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token);
            if (user == null || user.PasswordResetExpires == null || user.PasswordResetExpires < DateTime.UtcNow)
                return BadRequest(new { message = "This reset link is invalid or has expired. Please request a new one." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 11);
            user.PasswordResetToken = null;
            user.PasswordResetExpires = null;
            await _db.SaveChangesAsync();

            await _email.SendAsync(user.Email,
                "Your password was changed",
                "<p>Your Hospital Appointment System password was successfully changed. If this wasn't you, contact the clinic immediately.</p>");

            return Ok(new { message = "Password updated. You can now sign in." });
        }

        [HttpPost("seed-demo")]
        public async Task<IActionResult> SeedDemo()
        {
            if (await _db.Users.AnyAsync())
                return BadRequest(new { message = "Users already exist. Seed only runs on an empty database." });

            var facility = new Facility { Name = "Main Campus", Address = "1 Hospital Road", Phone = "08000000000" };
            _db.Facilities.Add(facility);
            await _db.SaveChangesAsync();

            var demoUsers = new (string name, string email, string pass, UserRole role)[]
            {
                ("System Admin",     "admin@hospital.com",      "admin123",   UserRole.Admin),
                ("Dr. Amara Chukwu", "amara@hospital.com",      "doctor123",  UserRole.Doctor),
                ("Test Patient",     "patient@hospital.com",    "patient123", UserRole.Patient),
                ("Pharm Tobi",       "pharmacist@hospital.com", "pharm123",   UserRole.Pharmacist),
                ("Lab Tech Chidi",   "labtech@hospital.com",    "lab123",     UserRole.LabTechnician),
            };

            foreach (var (name, email, pass, role) in demoUsers)
            {
                var user = new User
                {
                    FullName = name,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(pass, workFactor: 11),
                    Role = role,
                    IsActive = true,
                    EmailConfirmed = true
                };
                _db.Users.Add(user);
                await _db.SaveChangesAsync();

                if (role == UserRole.Doctor)
                {
                    _db.DoctorProfiles.Add(new DoctorProfile
                    {
                        UserId = user.Id,
                        Specialization = "General Medicine",
                        Bio = "General practitioner with 10+ years of experience.",
                        ConsultationFee = 5000.00m,
                        SlotDurationMinutes = 30,
                        FacilityId = facility.Id,
                        IsAcceptingBookings = true
                    });
                    await _db.SaveChangesAsync();

                    // Mon-Fri 9:00 AM to 6:00 PM (last slot starts at 5:30 PM)
                    for (int day = 1; day <= 5; day++)
                        _db.DoctorSchedules.Add(new DoctorSchedule
                        {
                            DoctorUserId = user.Id,
                            DayOfWeek = day,
                            StartTime = new TimeSpan(9, 0, 0),
                            EndTime = new TimeSpan(18, 0, 0)
                        });
                    await _db.SaveChangesAsync();
                }
                if (role == UserRole.Patient)
                {
                    _db.PatientProfiles.Add(new PatientProfile { UserId = user.Id });
                    await _db.SaveChangesAsync();
                }
            }

            return Ok(new
            {
                message = "Demo accounts, facility and doctor availability created.",
                accounts = demoUsers.Select(d => new { d.email, d.pass, role = d.role.ToString() })
            });
        }
    }
}