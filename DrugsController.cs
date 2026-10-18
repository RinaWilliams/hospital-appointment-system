using HospitalApi.Data;
using HospitalApi.DTOs;
using HospitalApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/drugs")]
    [Authorize]
    public class DrugsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public DrugsController(AppDbContext db) => _db = db;

        // Full list (used by formulary tab in admin + pharmacist).
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
        {
            var query = _db.Drugs.AsQueryable();
            if (activeOnly) query = query.Where(d => d.IsActive);
            return Ok(await query.OrderBy(d => d.Name).ToListAsync());
        }

        // Item xiv: autocomplete search. Matches name OR strength OR form.
        // Returns up to 10 results ordered by relevance and name.
        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
                return Ok(Array.Empty<object>());

            var term = q.Trim().ToLower();
            if (limit < 1) limit = 1;
            if (limit > 25) limit = 25;

            var matches = await _db.Drugs
                .Where(d => d.IsActive &&
                            (d.Name.ToLower().Contains(term)
                             || (d.Strength != null && d.Strength.ToLower().Contains(term))
                             || (d.Form != null && d.Form.ToLower().Contains(term))))
                .OrderBy(d => d.Name.StartsWith(term) ? 0 : 1)
                .ThenBy(d => d.Name)
                .Take(limit)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.Form,
                    d.Strength,
                    Display = d.Strength == null && d.Form == null
                        ? d.Name
                        : $"{d.Name} {(d.Strength ?? "")} {(d.Form ?? "")}".Trim()
                })
                .ToListAsync();

            return Ok(matches);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Pharmacist")]
        public async Task<IActionResult> Create(CreateDrugRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Drug name is required." });

            var name = request.Name.Trim();

            // Prevent exact duplicates (same name + strength + form).
            var dup = await _db.Drugs.AnyAsync(d =>
                d.Name.ToLower() == name.ToLower()
                && (d.Strength ?? "") == (request.Strength ?? "")
                && (d.Form ?? "") == (request.Form ?? ""));
            if (dup) return Conflict(new { message = "That drug (with the same strength and form) already exists." });

            var drug = new Drug
            {
                Name = name,
                Form = string.IsNullOrWhiteSpace(request.Form) ? null : request.Form!.Trim(),
                Strength = string.IsNullOrWhiteSpace(request.Strength) ? null : request.Strength!.Trim(),
                IsActive = true
            };
            _db.Drugs.Add(drug);
            await _db.SaveChangesAsync();
            return Ok(drug);
        }

        [HttpPut("{id}/toggle")]
        [Authorize(Roles = "Admin,Pharmacist")]
        public async Task<IActionResult> Toggle(int id, ToggleActiveRequest request)
        {
            var drug = await _db.Drugs.FindAsync(id);
            if (drug == null) return NotFound();
            drug.IsActive = request.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Updated." });
        }
    }
}