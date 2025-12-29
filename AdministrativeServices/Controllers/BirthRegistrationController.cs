using System;
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
    public class BirthRegistrationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BirthRegistrationController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            // Get current user info to auto-fill parent data
            var user = await _userManager.GetUserAsync(User);
            ViewBag.ParentName = user?.FullName;
            ViewBag.ParentCCCD = user?.CCCD;
            
            // Get or create service type
            var service = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Name == "Đăng ký khai sinh");
            if (service == null)
            {
                service = new ServiceType 
                { 
                    Name = "Đăng ký khai sinh", 
                    Description = "Đăng ký khai sinh cho trẻ em mới sinh", 
                    Fee = 0 // Miễn phí
                };
                _context.ServiceTypes.Add(service);
                await _context.SaveChangesAsync();
            }
            ViewBag.ServiceId = service.Id;
            
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            int serviceTypeId,
            string childFullName,
            string dateOfBirth,
            string placeOfBirth,
            string gender,
            string fatherName,
            string fatherCCCD,
            string motherName,
            string motherCCCD,
            List<IFormFile> files)
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var formData = new
            {
                ChildFullName = childFullName,
                DateOfBirth = dateOfBirth,
                PlaceOfBirth = placeOfBirth,
                Gender = gender,
                FatherName = fatherName,
                FatherCCCD = fatherCCCD,
                MotherName = motherName,
                MotherCCCD = motherCCCD
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
                        DocumentType = "BirthCertificateDoc"
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
                Note = "Hồ sơ đăng ký khai sinh đã được nộp trực tuyến",
                ChangedById = userId
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("Applications", "Citizen");
        }

        /// <summary>
        /// Generate unique citizen ID for newborn: ProvinceCode + GenderCode + YearOfBirth + RandomNumber
        /// </summary>
        public static string GenerateCitizenId(string provinceCode, string gender, int yearOfBirth)
        {
            var genderCode = gender == "Nam" ? "0" : "1";
            var yearCode = (yearOfBirth % 100).ToString("D2");
            var random = new Random().Next(100000, 999999);
            return $"{provinceCode}{genderCode}{yearCode}{random}";
        }
    }
}
