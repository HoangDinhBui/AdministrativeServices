using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using AdministrativeServices.Data;
using AdministrativeServices.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdministrativeServices.Controllers
{
    [Authorize(Roles = "Citizen")]
    public class TemporaryResidenceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TemporaryResidenceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.ApplicantName = user?.FullName;
            ViewBag.ApplicantCCCD = user?.CCCD;

            // Get or create service type
            var service = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Name == "Đăng ký tạm trú");
            if (service == null)
            {
                service = new ServiceType
                {
                    Name = "Đăng ký tạm trú",
                    Description = "Đăng ký tạm trú cho công dân",
                    Fee = 0 // Miễn phí
                };
                _context.ServiceTypes.Add(service);
                await _context.SaveChangesAsync();
            }
            ViewBag.ServiceId = service.Id;

            return View();
        }

        /// <summary>
        /// Lookup owner/landlord by CCCD
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> LookupOwner(string cccd)
        {
            if (string.IsNullOrEmpty(cccd))
            {
                return Json(new { success = false, message = "Vui lòng nhập số CCCD" });
            }

            // Check in Citizens table
            var citizen = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == cccd);
            if (citizen != null)
            {
                return Json(new
                {
                    success = true,
                    found = true,
                    hasAccount = true,
                    fullName = citizen.FullName,
                    cccd = citizen.CCCD,
                    message = "Chủ nhà có tài khoản trong hệ thống. Yêu cầu xác nhận sẽ được gửi tự động."
                });
            }

            // Check in ApplicationUser table
            var appUser = await _context.Users.FirstOrDefaultAsync(u => u.CCCD == cccd);
            if (appUser != null)
            {
                return Json(new
                {
                    success = true,
                    found = true,
                    hasAccount = true,
                    fullName = appUser.FullName,
                    cccd = appUser.CCCD,
                    message = "Chủ nhà có tài khoản trong hệ thống. Yêu cầu xác nhận sẽ được gửi tự động."
                });
            }

            return Json(new { 
                success = true, 
                found = false, 
                hasAccount = false,
                message = "Chủ nhà chưa có tài khoản. Cán bộ sẽ xác minh thủ công." 
            });
        }

        /// <summary>
        /// Auto-verification for temporary residence
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> VerifyEligibility(string applicantCCCD)
        {
            var results = new List<object>();

            var citizen = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == applicantCCCD);

            // 1. Check for existing active temporary residence
            var existingRegistration = await _context.TemporaryResidences
                .FirstOrDefaultAsync(t => t.CitizenCCCD == applicantCCCD && t.Status == "Active" && t.EndDate > DateTime.UtcNow);

            if (existingRegistration != null)
            {
                results.Add(new { 
                    check = "Tạm trú hiện tại", 
                    passed = false, 
                    message = $"Đang có đăng ký tạm trú tại: {existingRegistration.Address}. Cần xóa tạm trú cũ trước khi đăng ký mới." 
                });
            }
            else
            {
                results.Add(new { check = "Tạm trú hiện tại", passed = true, message = "Không có đăng ký tạm trú đang hiệu lực" });
            }

            // 2. Mock criminal record check
            // In real system, this would query a criminal database
            bool hasCriminalRecord = false; // Mock: always clean
            if (hasCriminalRecord)
            {
                results.Add(new { check = "Tiền án tiền sự", passed = false, message = "Có tiền án tiền sự - cần xác minh thêm" });
            }
            else
            {
                results.Add(new { check = "Tiền án tiền sự", passed = true, message = "Không có tiền án tiền sự" });
            }

            // 3. Check if citizen exists in system
            if (citizen != null)
            {
                results.Add(new { check = "Hồ sơ công dân", passed = true, message = "Có hồ sơ trong hệ thống quốc gia" });
            }
            else
            {
                results.Add(new { check = "Hồ sơ công dân", passed = true, message = "Chưa có trong hệ thống (sẽ tạo mới khi duyệt)" });
            }

            bool allPassed = results.All(r => ((dynamic)r).passed == true);
            return Json(new { success = true, allPassed, results });
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            int serviceTypeId,
            string registrationType, // New, Extend, Update
            string applicantName,
            string applicantCCCD,
            string applicantPhone,
            string province,
            string district,
            string ward,
            string addressDetail,
            string startDate,
            string endDate,
            string ownerName,
            string ownerCCCD,
            string ownerPhone,
            string pin,
            List<IFormFile> files)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            if (string.IsNullOrEmpty(pin) || pin.Length < 4)
            {
                TempData["ErrorMessage"] = "Mã PIN không hợp lệ.";
                return RedirectToAction(nameof(Create));
            }

            var fullAddress = $"{addressDetail}, {ward}, {district}, {province}";

            var formData = new
            {
                RegistrationType = registrationType,
                ApplicantName = applicantName,
                ApplicantCCCD = applicantCCCD,
                ApplicantPhone = applicantPhone,
                Province = province,
                District = district,
                Ward = ward,
                AddressDetail = addressDetail,
                FullAddress = fullAddress,
                StartDate = startDate,
                EndDate = endDate,
                OwnerName = ownerName,
                OwnerCCCD = ownerCCCD,
                OwnerPhone = ownerPhone,
                SubmittedAt = DateTime.UtcNow.ToString("o")
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

            // Handle file uploads
            if (files != null && files.Count > 0)
            {
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsDir))
                    Directory.CreateDirectory(uploadsDir);

                foreach (var file in files)
                {
                    var fileName = $"{application.Id}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var filePath = Path.Combine(uploadsDir, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var attachment = new Attachment
                    {
                        ApplicationId = application.Id,
                        FileName = file.FileName,
                        FilePath = $"/uploads/{fileName}",
                        DocumentType = "TemporaryResidenceDoc"
                    };
                    _context.Attachments.Add(attachment);
                }
                await _context.SaveChangesAsync();
            }

            // Check if owner has account and send confirmation request
            var ownerUser = await _context.Users.FirstOrDefaultAsync(u => u.CCCD == ownerCCCD);
            string historyNote = "Hồ sơ đăng ký tạm trú đã được nộp trực tuyến";
            if (ownerUser != null)
            {
                historyNote += ". Đã gửi yêu cầu xác nhận tới chủ nhà.";
                // In real system, send notification to owner
            }

            _context.ApplicationHistories.Add(new ApplicationHistory
            {
                ApplicationId = application.Id,
                Status = ApplicationStatus.Submitted,
                Note = historyNote,
                ChangedById = userId
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã nộp hồ sơ đăng ký tạm trú thành công!";
            return RedirectToAction("Applications", "Citizen");
        }
    }
}
