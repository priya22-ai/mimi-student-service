using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Models.ViewModels;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StaffController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> AllRequests()
        {
            var requests = await _context.ServiceRequests
                .Include(r => r.Student)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            var viewModels = requests.Select(r => new ServiceRequestDetailViewModel
            {
                Id = r.Id,
                RequestType = r.RequestType,
                Description = r.Description,
                Status = r.Status,
                CreatedDate = r.CreatedDate,
                UpdatedDate = r.UpdatedDate,
                StudentName = r.Student?.FirstName + " " + r.Student?.LastName,
                StudentEmail = r.Student?.Email ?? ""
            }).ToList();

            return View(viewModels);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var request = await _context.ServiceRequests
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            var viewModel = new ServiceRequestDetailViewModel
            {
                Id = request.Id,
                RequestType = request.RequestType,
                Description = request.Description,
                Status = request.Status,
                CreatedDate = request.CreatedDate,
                UpdatedDate = request.UpdatedDate,
                StudentName = request.Student?.FirstName + " " + request.Student?.LastName,
                StudentEmail = request.Student?.Email ?? ""
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, StaffUpdateViewModel model)
        {
            var request = await _context.ServiceRequests.FindAsync(id);
            if (request == null) return NotFound();

            request.Status = model.Status;
            request.UpdatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Status updated successfully!";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
