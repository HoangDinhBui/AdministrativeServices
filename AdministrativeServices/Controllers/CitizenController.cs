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

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var application = await _context.Applications
                .Include(a => a.ServiceType)
                .Include(a => a.Attachments)
                .Include(a => a.History)
                    .ThenInclude(h => h.ChangedBy)
                .FirstOrDefaultAsync(a => a.Id == id && a.CitizenId == userId);

            if (application == null) return NotFound();

            return View(application);
        }

        /// <summary>
        /// View pending confirmation requests (marriage, temporary residence)
        /// </summary>
        public async Task<IActionResult> ConfirmationRequests()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var requests = await _context.ConfirmationRequests
                .Include(r => r.Application)
                    .ThenInclude(a => a!.ServiceType)
                .Include(r => r.Requester)
                .Where(r => r.TargetUserId == userId && r.Status == "Pending")
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmMarriage(int requestId)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var request = await _context.ConfirmationRequests
                .Include(r => r.Application)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.TargetUserId == userId);

            if (request == null) return NotFound();

            // Update confirmation request
            request.Status = "Confirmed";
            request.ResponseDate = DateTime.UtcNow;

            // Update application status to Submitted
            if (request.Application != null)
            {
                request.Application.Status = ApplicationStatus.Submitted;
                request.Application.LastModifiedDate = DateTime.UtcNow;

                // Add history
                _context.ApplicationHistories.Add(new ApplicationHistory
                {
                    ApplicationId = request.ApplicationId,
                    Status = ApplicationStatus.Submitted,
                    Note = "Đối phương đã xác nhận đồng ý kết hôn. Hồ sơ được chuyển sang trạng thái Đã nộp.",
                    ChangedById = userId
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bạn đã xác nhận đồng ý kết hôn!";
            return RedirectToAction(nameof(ConfirmationRequests));
        }

        [HttpPost]
        public async Task<IActionResult> RejectConfirmation(int requestId, string reason)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var request = await _context.ConfirmationRequests
                .Include(r => r.Application)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.TargetUserId == userId);

            if (request == null) return NotFound();

            // Update confirmation request
            request.Status = "Rejected";
            request.RejectReason = reason;
            request.ResponseDate = DateTime.UtcNow;

            // Update application status to Rejected
            if (request.Application != null)
            {
                request.Application.Status = ApplicationStatus.Rejected;
                request.Application.RejectReason = $"Đối phương từ chối: {reason}";
                request.Application.LastModifiedDate = DateTime.UtcNow;

                _context.ApplicationHistories.Add(new ApplicationHistory
                {
                    ApplicationId = request.ApplicationId,
                    Status = ApplicationStatus.Rejected,
                    Note = $"Đối phương từ chối kết hôn: {reason}",
                    ChangedById = userId
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bạn đã từ chối yêu cầu.";
            return RedirectToAction(nameof(ConfirmationRequests));
        }

        /// <summary>
        /// Digital Wallet - Store and view approved documents
        /// </summary>
        public async Task<IActionResult> Wallet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var userCCCD = user.CCCD ?? "";

            // Get birth records where user is parent - deduplicate by RegistrationNumber
            var birthRecords = await _context.BirthRecords
                .Where(b => b.FatherCCCD == userCCCD || b.MotherCCCD == userCCCD)
                .GroupBy(b => b.RegistrationNumber)
                .Select(g => g.First())
                .ToListAsync();

            // Get marriage records - check both Citizens table and user's CCCD
            var marriageRecords = new List<MarriageRecord>();
            
            // First, try to find by Citizens table
            var citizen = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == userCCCD);
            if (citizen != null)
            {
                marriageRecords = await _context.MarriageRecords
                    .Include(m => m.Spouse1)
                    .Include(m => m.Spouse2)
                    .Where(m => (m.Spouse1Id == citizen.Id || m.Spouse2Id == citizen.Id) && m.Status == "Active")
                    .GroupBy(m => m.RegistrationNumber)
                    .Select(g => g.First())
                    .ToListAsync();
            }

            // Also check completed marriage applications if no records found
            if (!marriageRecords.Any())
            {
                // Get all marriage records and filter by checking if user's CCCD matches content
                var allMarriageRecords = await _context.MarriageRecords
                    .Include(m => m.Spouse1)
                    .Include(m => m.Spouse2)
                    .Where(m => m.Status == "Active")
                    .ToListAsync();
                
                // Check if user's CCCD is in Spouse1 or Spouse2
                marriageRecords = allMarriageRecords
                    .Where(m => m.Spouse1?.CCCD == userCCCD || m.Spouse2?.CCCD == userCCCD)
                    .GroupBy(m => m.RegistrationNumber)
                    .Select(g => g.First())
                    .ToList();
            }

            // Get temporary residence records - deduplicate
            var tempResidences = await _context.TemporaryResidences
                .Where(t => t.CitizenCCCD == userCCCD && t.Status == "Active")
                .GroupBy(t => t.RegistrationNumber)
                .Select(g => g.First())
                .ToListAsync();

            // Get completed applications for this user
            var completedApps = await _context.Applications
                .Include(a => a.ServiceType)
                .Where(a => a.CitizenId == user.Id && (a.Status == ApplicationStatus.Signed || a.Status == ApplicationStatus.Completed))
                .ToListAsync();

            ViewBag.BirthRecords = birthRecords;
            ViewBag.MarriageRecords = marriageRecords;
            ViewBag.TempResidences = tempResidences;
            ViewBag.CompletedApps = completedApps;
            ViewBag.UserCCCD = userCCCD;

            return View();
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
    }
}
