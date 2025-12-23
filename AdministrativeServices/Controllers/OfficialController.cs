using System;
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
                Note = note,
                ChangedById = currentUserId ?? ""
            });

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Inbox));
        }
    }
}
