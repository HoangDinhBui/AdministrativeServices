using AdministrativeServices.Data;
using AdministrativeServices.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AdministrativeServices.Controllers
{
    [Authorize(Roles = "Citizen")]
    public class CitizenController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CitizenController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Applications()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var applications = await _context.Applications
                .Include(a => a.ServiceType)
                .Where(a => a.CitizenId == userId)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return View(applications);
        }

        [HttpGet]
        public async Task<IActionResult> CreateResidentRegistration()
        {
            var service = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Name == "Đăng ký thường trú");
            if (service == null)
            {
                // Create default service type if not exists
                service = new ServiceType { Name = "Đăng ký thường trú", Description = "Đăng ký thường trú vào hộ gia đình hoặc lập hộ mới", Fee = 15000 };
                _context.ServiceTypes.Add(service);
                await _context.SaveChangesAsync();
            }
            ViewBag.ServiceId = service.Id;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateResidentRegistration(int serviceTypeId, string fullName, string dob, string gender, string cccd, string address, string relationship, string householdOwner, List<IFormFile> files)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var formData = new
            {
                FullName = fullName,
                DOB = dob,
                Gender = gender,
                CCCD = cccd,
                Address = address,
                Relationship = relationship,
                HouseholdOwner = householdOwner
            };

            var application = new Application
            {
                CitizenId = userId,
                ServiceTypeId = serviceTypeId,
                ContentJson = JsonSerializer.Serialize(formData),
                Status = ApplicationStatus.Submitted,
                CreatedDate = DateTime.UtcNow
            };

            _context.Applications.Add(application);
            await _context.SaveChangesAsync();

            // Handle file uploads (Simulated)
            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    var attachment = new Attachment
                    {
                        ApplicationId = application.Id,
                        FileName = file.FileName,
                        FilePath = "/uploads/" + file.FileName, // In real app, save to disk
                        DocumentType = "SupportingDocument"
                    };
                    _context.Attachments.Add(attachment);
                }
                await _context.SaveChangesAsync();
            }

            // Add history
            _context.ApplicationHistories.Add(new ApplicationHistory
            {
                ApplicationId = application.Id,
                Status = ApplicationStatus.Submitted,
                Note = "Hồ sơ đã được dân gửi trực tuyến",
                ChangedById = userId
            });
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Applications));
        }

        public async Task<IActionResult> Wallet()
        {
            var user = await _userManager.GetUserAsync(User);
            // Mock wallet data based on user profile
            return View(user);
        }
    }
}
