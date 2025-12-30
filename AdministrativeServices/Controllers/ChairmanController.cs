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
    [Authorize(Roles = "Chairman")]
    public class ChairmanController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChairmanController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Queue()
        {
            var applications = await _context.Applications
                .Include(a => a.ServiceType)
                .Include(a => a.Citizen)
                .Where(a => a.Status == ApplicationStatus.PendingApproval)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            return View(applications);
        }

        public async Task<IActionResult> Review(int id)
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
        public async Task<IActionResult> Sign(int id, string note)
        {
            var application = await _context.Applications.FindAsync(id);
            if (application == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            application.Status = ApplicationStatus.Signed;
            application.LastModifiedDate = DateTime.UtcNow;

            _context.ApplicationHistories.Add(new ApplicationHistory
            {
                ApplicationId = id,
                Status = ApplicationStatus.Signed,
                Note = string.IsNullOrEmpty(note) ? "Lãnh đạo đã ký duyệt hồ sơ" : note,
                ChangedById = currentUserId ?? ""
            });

            await _context.SaveChangesAsync();

            // If this is a birth registration, create birth record
            var birthService = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Name == "Đăng ký khai sinh");
            if (birthService != null && application.ServiceTypeId == birthService.Id)
            {
                await CreateBirthRecordFromApplication(application, currentUserId);
            }

            // If this is a marriage registration, create marriage record
            var marriageService = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Name == "Đăng ký kết hôn");
            if (marriageService != null && application.ServiceTypeId == marriageService.Id)
            {
                await CreateMarriageRecordFromApplication(application, currentUserId);
            }

            // If this is a temporary residence registration, create record
            var tempResService = await _context.ServiceTypes.FirstOrDefaultAsync(s => s.Name == "Đăng ký tạm trú");
            if (tempResService != null && application.ServiceTypeId == tempResService.Id)
            {
                await CreateTemporaryResidenceFromApplication(application, currentUserId);
            }

            TempData["SuccessMessage"] = $"Đã ký duyệt thành công hồ sơ #{id:D5}!";
            return RedirectToAction(nameof(Queue));
        }

        [HttpPost]
        public async Task<IActionResult> Complete(int id)
        {
            var application = await _context.Applications.FindAsync(id);
            if (application == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            application.Status = ApplicationStatus.Completed;
            application.LastModifiedDate = DateTime.UtcNow;

            _context.ApplicationHistories.Add(new ApplicationHistory
            {
                ApplicationId = id,
                Status = ApplicationStatus.Completed,
                Note = "Hồ sơ đã hoàn thành, sẵn sàng trả kết quả cho công dân",
                ChangedById = currentUserId ?? ""
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã hoàn thành hồ sơ #{id:D5}! Sẵn sàng trả kết quả cho công dân.";
            return RedirectToAction(nameof(Queue));
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var application = await _context.Applications.FindAsync(id);
            if (application == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);

            application.Status = ApplicationStatus.Rejected;
            application.RejectReason = reason;
            application.LastModifiedDate = DateTime.UtcNow;

            _context.ApplicationHistories.Add(new ApplicationHistory
            {
                ApplicationId = id,
                Status = ApplicationStatus.Rejected,
                Note = $"Lãnh đạo từ chối: {reason}",
                ChangedById = currentUserId ?? ""
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã từ chối hồ sơ #{id:D5}.";
            return RedirectToAction(nameof(Queue));
        }

        private async Task CreateBirthRecordFromApplication(Application application, string? signedById)
        {
            try
            {
                var formData = JsonSerializer.Deserialize<JsonElement>(application.ContentJson);
                
                var birthRecord = new BirthRecord
                {
                    RegistrationNumber = $"KS-{DateTime.Now.Year}-{application.Id:D6}",
                    GeneratedCitizenId = BirthRegistrationController.GenerateCitizenId("001", formData.GetProperty("Gender").GetString() ?? "Nam", DateTime.Now.Year),
                    ChildFullName = formData.GetProperty("ChildFullName").GetString() ?? "",
                    DateOfBirth = DateTime.Parse(formData.GetProperty("DateOfBirth").GetString() ?? DateTime.Now.ToString()),
                    PlaceOfBirth = formData.GetProperty("PlaceOfBirth").GetString() ?? "",
                    Gender = formData.GetProperty("Gender").GetString() ?? "",
                    FatherCCCD = formData.GetProperty("FatherCCCD").GetString(),
                    FatherName = formData.GetProperty("FatherName").GetString(),
                    MotherCCCD = formData.GetProperty("MotherCCCD").GetString(),
                    MotherName = formData.GetProperty("MotherName").GetString(),
                    RegistrationDate = DateTime.UtcNow,
                    SignedByChairmanId = signedById,
                    SignedDate = DateTime.UtcNow
                };

                _context.BirthRecords.Add(birthRecord);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Log error but don't fail the signing process
            }
        }

        private async Task CreateMarriageRecordFromApplication(Application application, string? signedById)
        {
            try
            {
                var formData = JsonSerializer.Deserialize<JsonElement>(application.ContentJson);

                var applicantCCCD = formData.GetProperty("ApplicantCCCD").GetString();
                var spouseCCCD = formData.GetProperty("SpouseCCCD").GetString();

                // Find or create citizens in the system
                var spouse1 = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == applicantCCCD);
                var spouse2 = await _context.Citizens.FirstOrDefaultAsync(c => c.CCCD == spouseCCCD);

                int? spouse1Id = spouse1?.Id;
                int? spouse2Id = spouse2?.Id;

                // Create citizen records if they don't exist
                if (spouse1 == null)
                {
                    var newCitizen = new Citizen
                    {
                        CCCD = applicantCCCD ?? "",
                        FullName = formData.GetProperty("ApplicantName").GetString() ?? "",
                        DateOfBirth = DateTime.Parse(formData.GetProperty("ApplicantDOB").GetString() ?? DateTime.Now.ToString()),
                        Gender = formData.GetProperty("ApplicantGender").GetString() ?? "",
                        MaritalStatus = "Đã kết hôn"
                    };
                    _context.Citizens.Add(newCitizen);
                    await _context.SaveChangesAsync();
                    spouse1Id = newCitizen.Id;
                }
                else
                {
                    spouse1.MaritalStatus = "Đã kết hôn";
                }

                if (spouse2 == null)
                {
                    var newCitizen = new Citizen
                    {
                        CCCD = spouseCCCD ?? "",
                        FullName = formData.GetProperty("SpouseName").GetString() ?? "",
                        DateOfBirth = DateTime.Parse(formData.GetProperty("SpouseDOB").GetString() ?? DateTime.Now.ToString()),
                        Gender = formData.GetProperty("SpouseGender").GetString() ?? "",
                        MaritalStatus = "Đã kết hôn"
                    };
                    _context.Citizens.Add(newCitizen);
                    await _context.SaveChangesAsync();
                    spouse2Id = newCitizen.Id;
                }
                else
                {
                    spouse2.MaritalStatus = "Đã kết hôn";
                }

                // Create marriage record
                var marriageRecord = new MarriageRecord
                {
                    RegistrationNumber = $"KH-{DateTime.Now.Year}-{application.Id:D6}",
                    Spouse1Id = spouse1Id ?? 0,
                    Spouse2Id = spouse2Id ?? 0,
                    MarriageDate = DateTime.UtcNow,
                    RegistrationPlace = "Hệ thống Dịch vụ Công trực tuyến",
                    Status = "Active",
                    CreatedDate = DateTime.UtcNow
                };

                _context.MarriageRecords.Add(marriageRecord);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Log error but don't fail the signing process
            }
        }

        private async Task CreateTemporaryResidenceFromApplication(Application application, string? signedById)
        {
            try
            {
                var formData = JsonSerializer.Deserialize<JsonElement>(application.ContentJson);

                var tempResidence = new TemporaryResidence
                {
                    RegistrationNumber = $"TT-{DateTime.Now.Year}-{application.Id:D6}",
                    CitizenCCCD = formData.GetProperty("ApplicantCCCD").GetString() ?? "",
                    CitizenName = formData.GetProperty("ApplicantName").GetString() ?? "",
                    CitizenPhone = formData.GetProperty("ApplicantPhone").GetString() ?? "",
                    Province = formData.GetProperty("Province").GetString() ?? "",
                    District = formData.GetProperty("District").GetString() ?? "",
                    Ward = formData.GetProperty("Ward").GetString() ?? "",
                    Address = formData.GetProperty("FullAddress").GetString() ?? "",
                    StartDate = DateTime.Parse(formData.GetProperty("StartDate").GetString() ?? DateTime.Now.ToString()),
                    EndDate = DateTime.Parse(formData.GetProperty("EndDate").GetString() ?? DateTime.Now.AddYears(2).ToString()),
                    OwnerCCCD = formData.GetProperty("OwnerCCCD").GetString() ?? "",
                    OwnerName = formData.GetProperty("OwnerName").GetString() ?? "",
                    OwnerPhone = formData.GetProperty("OwnerPhone").GetString() ?? "",
                    RegistrationType = formData.GetProperty("RegistrationType").GetString() ?? "New",
                    Status = "Active",
                    SignedByOfficialId = signedById,
                    SignedDate = DateTime.UtcNow
                };

                _context.TemporaryResidences.Add(tempResidence);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Log error but don't fail the signing process
            }
        }
    }
}
