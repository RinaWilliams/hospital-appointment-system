using HospitalApi.Data;
using HospitalApi.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalApi.Controllers
{
    [ApiController]
    [Route("api/facilities")]
    public class FacilitiesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public FacilitiesController(AppDbContext db) => _db = db;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll() => Ok(await _db.Facilities.ToListAsync());

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateFacilityRequest request)
        {
            var facility = new Models.Facility { Name = request.Name, Address = request.Address, Phone = request.Phone };
            _db.Facilities.Add(facility);
            await _db.SaveChangesAsync();
            return Ok(facility);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var facility = await _db.Facilities.FindAsync(id);
            if (facility == null) return NotFound();
            _db.Facilities.Remove(facility);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
