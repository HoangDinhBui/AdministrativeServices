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
    [Authorize(Roles = "Official,Admin")]
    public class OfficialController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OfficialController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Inbox()
        {
            var applications = await _context.Applications
                .Include(a => a.ServiceType)
                .Include(a => a.Citizen)
                .Where(a => a.Status != ApplicationStatus.Draft && a.Status != ApplicationStatus.Completed && a.Status != ApplicationStatus.Rejected)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return View(applications);
        }

        public async Task<IActionResult> Details(int id)
        {
            var application = await _context.Applications
                .Include(a => a.ServiceType)
                .Include(a => a.Citizen)
                .Include(a => a.Attachments)
                .Include(a => a.History)
                    .ThenInclude(h => h.ChangedBy)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null) return NotFound();

            return View(application);
        }

        [HttpPost]
        public async Task<IActionResult> Process(int id, ApplicationStatus nextStatus, string note)
        {
            var application = await _context.Applications.FindAsync(id);
            if (application == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            application.Status = nextStatus;
            application.LastModifiedDate = DateTime.UtcNow;
            application.CurrentOfficialId = currentUserId;

            _context.ApplicationHistories.Add(new ApplicationHistory
            {
                ApplicationId = id,
                Status = nextStatus,
                Note = string.IsNullOrEmpty(note) ? $"Chuyển trạng thái sang: {nextStatus}" : note,
                ChangedById = currentUserId ?? ""
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Inbox));
        }

        // ========== INTERNAL SEARCH ==========

        public IActionResult Search()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Search(string cccd)
        {
            if (string.IsNullOrEmpty(cccd))
            {
                ViewBag.Error = "Vui lòng nhập số CCCD";
                return View();
            }

            var citizen = await _context.Citizens
                .Include(c => c.CurrentHousehold)
                .Include(c => c.Father)
                .Include(c => c.Mother)
                .FirstOrDefaultAsync(c => c.CCCD == cccd);

            if (citizen == null)
            {
                ViewBag.Error = "Không tìm thấy công dân với CCCD: " + cccd;
                return View();
            }

            return RedirectToAction(nameof(CitizenProfile), new { id = citizen.Id });
        }

        public async Task<IActionResult> CitizenProfile(int id)
        {
            var citizen = await _context.Citizens
                .Include(c => c.CurrentHousehold)
                .Include(c => c.Father)
                .Include(c => c.Mother)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (citizen == null) return NotFound();

            // Get marriage records
            var marriages = await _context.MarriageRecords
                .Include(m => m.Spouse1)
                .Include(m => m.Spouse2)
                .Where(m => m.Spouse1Id == id || m.Spouse2Id == id)
                .ToListAsync();

            // Get birth records where this person is parent
            var childrenRecords = await _context.BirthRecords
                .Where(b => b.FatherId == id || b.MotherId == id)
                .ToListAsync();

            // Get household members if in a household
            var householdMembers = citizen.CurrentHouseholdId.HasValue
                ? await _context.HouseholdMembers
                    .Include(hm => hm.Citizen)
                    .Where(hm => hm.HouseholdId == citizen.CurrentHouseholdId && hm.IsCurrentMember)
                    .ToListAsync()
                : new List<HouseholdMember>();

            ViewBag.Marriages = marriages;
            ViewBag.Children = childrenRecords;
            ViewBag.HouseholdMembers = householdMembers;

            return View(citizen);
        }

        /// <summary>
        /// Check marriage status between two CCCDs
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CheckMarriage(string cccd1, string cccd2)
        {
            var citizen1 = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == cccd1);
            var citizen2 = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == cccd2);

            if (citizen1 == null || citizen2 == null)
            {
                return Json(new { 
                    success = false, 
                    message = "Không tìm thấy một hoặc cả hai công dân trong hệ thống" 
                });
            }

            var marriage = await _context.MarriageRecords
                .FirstOrDefaultAsync(m => 
                    (m.Spouse1Id == citizen1.Id && m.Spouse2Id == citizen2.Id) ||
                    (m.Spouse1Id == citizen2.Id && m.Spouse2Id == citizen1.Id));

            if (marriage != null && marriage.Status == "Active")
            {
                return Json(new { 
                    success = true, 
                    married = true,
                    message = $"Xác nhận: {citizen1.FullName} và {citizen2.FullName} đã đăng ký kết hôn ngày {marriage.MarriageDate:dd/MM/yyyy}",
                    marriageDate = marriage.MarriageDate.ToString("dd/MM/yyyy"),
                    registrationNumber = marriage.RegistrationNumber
                });
            }

            return Json(new { 
                success = true, 
                married = false,
                message = "Không tìm thấy hồ sơ kết hôn giữa hai công dân này" 
            });
        }

        /// <summary>
        /// Quick check for birth registration - verify parents marriage
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> VerifyBirthRegistration(int applicationId)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null) return NotFound();

            try
            {
                var formData = JsonSerializer.Deserialize<JsonElement>(application.ContentJson);
                var fatherCCCD = formData.GetProperty("FatherCCCD").GetString();
                var motherCCCD = formData.GetProperty("MotherCCCD").GetString();

                if (string.IsNullOrEmpty(fatherCCCD) || string.IsNullOrEmpty(motherCCCD))
                {
                    return Json(new { success = false, message = "Thiếu thông tin CCCD cha/mẹ" });
                }

                var father = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == fatherCCCD);
                var mother = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == motherCCCD);

                if (father == null || mother == null)
                {
                    return Json(new { 
                        success = true, 
                        verified = false,
                        message = "Cha hoặc mẹ chưa có trong hệ thống. Cần yêu cầu bổ sung giấy chứng nhận kết hôn." 
                    });
                }

                var marriage = await _context.MarriageRecords
                    .FirstOrDefaultAsync(m => 
                        ((m.Spouse1Id == father.Id && m.Spouse2Id == mother.Id) ||
                         (m.Spouse1Id == mother.Id && m.Spouse2Id == father.Id)) &&
                        m.Status == "Active");

                if (marriage != null)
                {
                    return Json(new { 
                        success = true, 
                        verified = true,
                        message = $"Xác nhận: Cha mẹ đã đăng ký kết hôn ngày {marriage.MarriageDate:dd/MM/yyyy}. Hồ sơ đủ điều kiện.",
                        fatherName = father.FullName,
                        motherName = mother.FullName
                    });
                }

                return Json(new { 
                    success = true, 
                    verified = false,
                    message = "Không tìm thấy hồ sơ kết hôn. Cần yêu cầu bổ sung giấy chứng nhận kết hôn hoặc làm thủ tục nhận cha con." 
                });
            }
            catch
            {
                return Json(new { success = false, message = "Lỗi khi đọc dữ liệu hồ sơ" });
            }
        }
    }
}
