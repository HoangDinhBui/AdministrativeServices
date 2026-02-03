using AdministrativeServices.Data;
using AdministrativeServices.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdministrativeServices.Controllers
{
    [Authorize]
    public class IdentityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public IdentityController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        // GET: Identity/Index (Upload Form)
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // Check if already has a pending or verified request
            var existingRequest = await _context.IdentityVerificationRequests
                .Where(r => r.UserId == user.Id)
                .OrderByDescending(r => r.CreatedDate)
                .FirstOrDefaultAsync();

            if (existingRequest != null && existingRequest.Status != IdentityVerificationStatus.Rejected && existingRequest.Status != IdentityVerificationStatus.SupplementRequired)
            {
                return View("Status", existingRequest);
            }

            if (user.IdentityStatus == IdentityVerificationStatus.Verified)
            {
                return View("Status", new IdentityVerificationRequest { Status = IdentityVerificationStatus.Verified });
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Upload(string cccd, IFormFile frontImage, IFormFile backImage, IFormFile portraitImage)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(cccd) || frontImage == null || backImage == null || portraitImage == null)
            {
                ModelState.AddModelError("", "Vui lòng nhập số CCCD và tải lên đầy đủ 3 loại giấy tờ.");
                return View("Index");
            }

            // Validate Extensions
            if (!IsValidFile(frontImage) || !IsValidFile(backImage) || !IsValidFile(portraitImage))
            {
                ModelState.AddModelError("", "Chỉ chấp nhận file ảnh (jpg, png) hoặc PDF.");
                return View("Index");
            }

            // Save files
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "identity");
            Directory.CreateDirectory(uploadsFolder);

            // Create Request
            var request = new IdentityVerificationRequest
            {
                UserId = user.Id,
                CCCD = cccd,
                Status = IdentityVerificationStatus.Pending,
                CreatedDate = DateTime.UtcNow
            };
            _context.IdentityVerificationRequests.Add(request);
            await _context.SaveChangesAsync(); // Save to get Id

            // Save Attachments
            string frontPath = await SaveFileAndCreateAttachment(frontImage, uploadsFolder, request.Id, "CCCD_Front");
            string backPath = await SaveFileAndCreateAttachment(backImage, uploadsFolder, request.Id, "CCCD_Back");
            string portraitPath = await SaveFileAndCreateAttachment(portraitImage, uploadsFolder, request.Id, "Portrait");

            // Update legacy columns (optional, but good for display in Index if logic uses them)
            request.FrontImage = frontPath;
            request.BackImage = backPath;
            request.PortraitImage = portraitPath;
            _context.Update(request);

            // Update User status
            user.IdentityStatus = IdentityVerificationStatus.Pending;
            user.CCCD = cccd; 
            await _userManager.UpdateAsync(user);
            
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool IsValidFile(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".pdf";
        }

        private async Task<string> SaveFileAndCreateAttachment(IFormFile file, string folder, int requestId, string docType)
        {
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(folder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }
            string relativePath = "/uploads/identity/" + uniqueFileName;

            var attachment = new IdentityAttachment
            {
                IdentityVerificationRequestId = requestId,
                FileName = file.FileName,
                FilePath = relativePath,
                ContentType = file.ContentType,
                DocumentType = docType
            };
            _context.IdentityAttachments.Add(attachment);

            return relativePath;
        }

        // GET: Identity/Manage (For Officials)
        [Authorize(Roles = "Official,Admin,Chairman")]
        public async Task<IActionResult> Manage()
        {
            var requests = await _context.IdentityVerificationRequests
                .Include(r => r.User)
                .Where(r => r.Status == IdentityVerificationStatus.Pending)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();
            return View(requests);
        }

        // GET: Identity/Review/5
        [Authorize(Roles = "Official,Admin,Chairman")]
        public async Task<IActionResult> Review(int id)
        {
            var request = await _context.IdentityVerificationRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            // Find matching citizen in DB
            var citizen = await _context.Citizens
                .FirstOrDefaultAsync(c => c.CCCD == request.CCCD);

            ViewBag.Citizen = citizen;
            return View(request);
        }

        // POST: Identity/Approve/5
        [HttpPost]
        [Authorize(Roles = "Official,Admin,Chairman")]
        public async Task<IActionResult> Approve(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await _context.IdentityVerificationRequests
                    .Include(r => r.User)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (request == null) return NotFound();

                var citizen = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == request.CCCD);
                if (citizen == null)
                {
                    // In a real scenario, we might create a new citizen record or require manual entry.
                    // For now, we reject if not found in national DB.
                    request.Status = IdentityVerificationStatus.Rejected;
                    request.RejectReason = "Không tìm thấy dữ liệu công dân trong hệ thống quốc gia.";
                    request.ProcessedDate = DateTime.UtcNow;
                    request.ProcessedByUserId = _userManager.GetUserId(User);
                    
                    request.User.IdentityStatus = IdentityVerificationStatus.Rejected;
                    await _userManager.UpdateAsync(request.User);
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    
                    TempData["ErrorMessage"] = "Không tìm thấy công dân. Đã từ chối yêu cầu.";
                    return RedirectToAction(nameof(Manage));
                }

                // Approve
                request.Status = IdentityVerificationStatus.Verified;
                request.ProcessedDate = DateTime.UtcNow;
                request.ProcessedByUserId = _userManager.GetUserId(User);

                // Link User to Citizen
                var user = request.User;
                user.IdentityStatus = IdentityVerificationStatus.Verified;
                user.CitizenProfileId = citizen.Id;
                user.FullName = citizen.FullName; // Sync name
                user.CCCD = citizen.CCCD;
                user.Street = citizen.PermanentAddress;
                // user.Ward = ...; // Need to parse later 

                await _userManager.UpdateAsync(user);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Đã phê duyệt xác thực thành công.";
                return RedirectToAction(nameof(Manage));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
                return RedirectToAction(nameof(Review), new { id });
            }
        }

        // POST: Identity/Reject/5
        [HttpPost]
        [Authorize(Roles = "Official,Admin,Chairman")]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var request = await _context.IdentityVerificationRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound();

            request.Status = IdentityVerificationStatus.Rejected;
            request.RejectReason = reason;
            request.ProcessedDate = DateTime.UtcNow;
            request.ProcessedByUserId = _userManager.GetUserId(User);

            var user = request.User;
            user.IdentityStatus = IdentityVerificationStatus.Rejected;
            await _userManager.UpdateAsync(user);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã từ chối yêu cầu.";
            return RedirectToAction(nameof(Manage));
        }
    }
}
