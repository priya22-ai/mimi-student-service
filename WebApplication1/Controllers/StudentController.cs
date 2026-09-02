using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Models.ViewModels;

namespace WebApplication1.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public StudentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> MyRequests()
        {
            var studentId = _userManager.GetUserId(User);
            var requests = await _context.ServiceRequests
                .Where(r => r.StudentId == studentId)
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
                StudentName = "",
                StudentEmail = ""
            }).ToList();

            return View(viewModels);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ServiceRequestViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceRequestViewModel model)
        {
            if (ModelState.IsValid)
            {
                var studentId = _userManager.GetUserId(User);
                var serviceRequest = new ServiceRequest
                {
                    StudentId = studentId!,
                    RequestType = model.RequestType,
                    Description = model.Description,
                    Status = RequestStatus.Pending,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };

                _context.ServiceRequests.Add(serviceRequest);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Request submitted successfully!";
                return RedirectToAction(nameof(MyRequests));
            }
            return View(model);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var studentId = _userManager.GetUserId(User);
            var request = await _context.ServiceRequests
                .Include(r => r.Student)
                .FirstOrDefaultAsync(r => r.Id == id && r.StudentId == studentId);

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
    }
}
