using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AdministrativeServices.Data;
using AdministrativeServices.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdministrativeServices.Controllers
{
    [Authorize(Roles = "Citizen")]
    public class MarriageRegistrationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MarriageRegistrationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
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
            var service = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Name == "Đăng ký kết hôn");
            if (service == null)
            {
                service = new ServiceType
                {
                    Name = "Đăng ký kết hôn",
                    Description = "Đăng ký kết hôn cho công dân Việt Nam",
                    Fee = 50000 // 50,000 VND
                };
                _context.ServiceTypes.Add(service);
                await _context.SaveChangesAsync();
            }
            ViewBag.ServiceId = service.Id;
            ViewBag.Fee = service.Fee;

            return View();
        }

        /// <summary>
        /// AJAX endpoint to lookup spouse info by CCCD
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> LookupSpouse(string cccd)
        {
            if (string.IsNullOrEmpty(cccd))
            {
                return Json(new { success = false, message = "Vui lòng nhập số CCCD" });
            }

            var citizen = await _context.Citizens
                .Include(c => c.CurrentHousehold)
                .FirstOrDefaultAsync(c => c.CCCD == cccd);

            if (citizen == null)
            {
                // Check in ApplicationUser if not in Citizens
                var appUser = await _context.Users.FirstOrDefaultAsync(u => u.CCCD == cccd);
                if (appUser != null)
                {
                    return Json(new
                    {
                        success = true,
                        found = true,
                        fromSystem = false, // From user registration, not full citizen record
                        fullName = appUser.FullName,
                        cccd = appUser.CCCD,
                        address = appUser.Address,
                        message = "Tìm thấy thông tin từ hồ sơ đăng ký người dùng"
                    });
                }

                return Json(new { success = true, found = false, message = "Không tìm thấy công dân trong hệ thống. Vui lòng nhập thông tin thủ công." });
            }

            return Json(new
            {
                success = true,
                found = true,
                fromSystem = true,
                fullName = citizen.FullName,
                cccd = citizen.CCCD,
                dateOfBirth = citizen.DateOfBirth.ToString("yyyy-MM-dd"),
                gender = citizen.Gender,
                placeOfBirth = citizen.PlaceOfBirth,
                maritalStatus = citizen.MaritalStatus,
                address = citizen.CurrentHousehold?.Address,
                message = "Tìm thấy thông tin công dân"
            });
        }

        /// <summary>
        /// Auto-verification checks before submission
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> VerifyEligibility(string applicantCCCD, string spouseCCCD, string applicantGender, string spouseGender, string applicantDOB, string spouseDOB)
        {
            var results = new List<object>();

            // 1. Age Check
            var applicantAge = CalculateAge(DateTime.Parse(applicantDOB));
            var spouseAge = CalculateAge(DateTime.Parse(spouseDOB));

            int requiredAgeApplicant = applicantGender == "Nam" ? 20 : 18;
            int requiredAgeSpouse = spouseGender == "Nam" ? 20 : 18;

            if (applicantAge < requiredAgeApplicant)
            {
                results.Add(new { check = "Tuổi người nộp", passed = false, message = $"Chưa đủ tuổi kết hôn. Yêu cầu: {requiredAgeApplicant}, Hiện tại: {applicantAge}" });
            }
            else
            {
                results.Add(new { check = "Tuổi người nộp", passed = true, message = $"Đủ tuổi kết hôn ({applicantAge} tuổi)" });
            }

            if (spouseAge < requiredAgeSpouse)
            {
                results.Add(new { check = "Tuổi vợ/chồng", passed = false, message = $"Chưa đủ tuổi kết hôn. Yêu cầu: {requiredAgeSpouse}, Hiện tại: {spouseAge}" });
            }
            else
            {
                results.Add(new { check = "Tuổi vợ/chồng", passed = true, message = $"Đủ tuổi kết hôn ({spouseAge} tuổi)" });
            }

            // 2. Marriage Status Check
            var applicantCitizen = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == applicantCCCD);
            var spouseCitizen = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == spouseCCCD);

            if (applicantCitizen != null)
            {
                var existingMarriage = await _context.MarriageRecords
                    .FirstOrDefaultAsync(m => (m.Spouse1Id == applicantCitizen.Id || m.Spouse2Id == applicantCitizen.Id) && m.Status == "Active");

                if (existingMarriage != null)
                {
                    results.Add(new { check = "Tình trạng hôn nhân người nộp", passed = false, message = "Vi phạm chế độ một vợ một chồng - đã có hôn nhân đang hiệu lực" });
                }
                else
                {
                    results.Add(new { check = "Tình trạng hôn nhân người nộp", passed = true, message = $"Không có hôn nhân đang hiệu lực" });
                }
            }
            else
            {
                results.Add(new { check = "Tình trạng hôn nhân người nộp", passed = true, message = "Chưa có hồ sơ trong hệ thống (cần xác minh thủ công)" });
            }

            if (spouseCitizen != null)
            {
                var existingMarriage = await _context.MarriageRecords
                    .FirstOrDefaultAsync(m => (m.Spouse1Id == spouseCitizen.Id || m.Spouse2Id == spouseCitizen.Id) && m.Status == "Active");

                if (existingMarriage != null)
                {
                    results.Add(new { check = "Tình trạng hôn nhân vợ/chồng", passed = false, message = "Vi phạm chế độ một vợ một chồng - đã có hôn nhân đang hiệu lực" });
                }
                else
                {
                    results.Add(new { check = "Tình trạng hôn nhân vợ/chồng", passed = true, message = "Không có hôn nhân đang hiệu lực" });
                }
            }
            else
            {
                results.Add(new { check = "Tình trạng hôn nhân vợ/chồng", passed = true, message = "Chưa có hồ sơ trong hệ thống (cần xác minh thủ công)" });
            }

            // 3. Blood Relation Check (if both in system)
            if (applicantCitizen != null && spouseCitizen != null)
            {
                bool isRelated = false;
                string relationMessage = "";

                // Check same parents
                if (applicantCitizen.FatherId != null && spouseCitizen.FatherId != null && applicantCitizen.FatherId == spouseCitizen.FatherId)
                {
                    isRelated = true;
                    relationMessage = "Cùng cha";
                }
                if (applicantCitizen.MotherId != null && spouseCitizen.MotherId != null && applicantCitizen.MotherId == spouseCitizen.MotherId)
                {
                    isRelated = true;
                    relationMessage += string.IsNullOrEmpty(relationMessage) ? "Cùng mẹ" : ", cùng mẹ";
                }

                if (isRelated)
                {
                    results.Add(new { check = "Quan hệ huyết thống", passed = false, message = $"Phát hiện quan hệ huyết thống: {relationMessage}" });
                }
                else
                {
                    results.Add(new { check = "Quan hệ huyết thống", passed = true, message = "Không phát hiện quan hệ huyết thống trực tiếp" });
                }
            }
            else
            {
                results.Add(new { check = "Quan hệ huyết thống", passed = true, message = "Không đủ dữ liệu để kiểm tra tự động" });
            }

            bool allPassed = results.All(r => ((dynamic)r).passed == true);

            return Json(new { success = true, allPassed, results });
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            int serviceTypeId,
            string applicantName,
            string applicantCCCD,
            string applicantDOB,
            string applicantGender,
            string applicantAddress,
            string spouseName,
            string spouseCCCD,
            string spouseDOB,
            string spouseGender,
            string spouseAddress,
            string pin,
            List<IFormFile> files)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            // Verify PIN (in real system, this would be more secure)
            if (string.IsNullOrEmpty(pin) || pin.Length < 4)
            {
                TempData["ErrorMessage"] = "Mã PIN không hợp lệ. Vui lòng nhập ít nhất 4 ký tự.";
                return RedirectToAction(nameof(Create));
            }

            var formData = new
            {
                ApplicantName = applicantName,
                ApplicantCCCD = applicantCCCD,
                ApplicantDOB = applicantDOB,
                ApplicantGender = applicantGender,
                ApplicantAddress = applicantAddress,
                SpouseName = spouseName,
                SpouseCCCD = spouseCCCD,
                SpouseDOB = spouseDOB,
                SpouseGender = spouseGender,
                SpouseAddress = spouseAddress,
                SubmittedAt = DateTime.UtcNow.ToString("o")
            };

            // Check if spouse has an account in the system
            var spouseUser = await _context.Users.FirstOrDefaultAsync(u => u.CCCD == spouseCCCD);
            bool requiresSpouseConfirmation = spouseUser != null;

            var application = new Application
            {
                CitizenId = userId,
                ServiceTypeId = serviceTypeId,
                ContentJson = JsonSerializer.Serialize(formData),
                Status = requiresSpouseConfirmation ? ApplicationStatus.AwaitingConfirmation : ApplicationStatus.Submitted,
                CreatedDate = DateTime.UtcNow
            };

            _context.Applications.Add(application);
            await _context.SaveChangesAsync();

            // Create confirmation request if spouse has account
            if (requiresSpouseConfirmation && spouseUser != null)
            {
                var confirmRequest = new ConfirmationRequest
                {
                    ApplicationId = application.Id,
                    RequesterId = userId,
                    TargetUserId = spouseUser.Id,
                    TargetCCCD = spouseCCCD,
                    RequestType = "Marriage"
                };
                _context.ConfirmationRequests.Add(confirmRequest);
                await _context.SaveChangesAsync();
            }

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
                        DocumentType = "MarriageDoc"
                    };
                    _context.Attachments.Add(attachment);
                }
                await _context.SaveChangesAsync();
            }

            // Add history
            string historyNote = requiresSpouseConfirmation 
                ? $"Hồ sơ đăng ký kết hôn đã được nộp. Đang chờ {spouseName} xác nhận."
                : "Hồ sơ đăng ký kết hôn đã được nộp trực tuyến";

            _context.ApplicationHistories.Add(new ApplicationHistory
            {
                ApplicationId = application.Id,
                Status = application.Status,
                Note = historyNote,
                ChangedById = userId
            });
            await _context.SaveChangesAsync();

            if (requiresSpouseConfirmation)
            {
                TempData["SuccessMessage"] = $"Đã nộp hồ sơ! Yêu cầu xác nhận đã được gửi tới {spouseName}.";
            }
            else
            {
                TempData["SuccessMessage"] = "Đã nộp hồ sơ đăng ký kết hôn thành công!";
            }
            return RedirectToAction("Applications", "Citizen");
        }

        private int CalculateAge(DateTime dateOfBirth)
        {
            var today = DateTime.Today;
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}
