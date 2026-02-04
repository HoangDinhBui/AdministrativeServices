using AdministrativeServices.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AdministrativeServices.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        public async Task<IActionResult> Register(string cccd, string phoneNumber)
        {
            if (string.IsNullOrEmpty(cccd) || string.IsNullOrEmpty(phoneNumber))
            {
                ModelState.AddModelError("", "Vui lòng nhập số CCCD và SĐT");
                return View();
            }

            // Check if user exists (CCCD)
            var existingUser = await _userManager.FindByNameAsync(cccd);
            if (existingUser != null)
            {
                ModelState.AddModelError("", "Số CCCD này đã được đăng ký.");
                return View();
            }

            // Check duplicate phone
            if (_userManager.Users.Any(u => u.PhoneNumber == phoneNumber))
            {
                ModelState.AddModelError("", "Số điện thoại này đã được sử dụng.");
                return View();
            }

            // In real app: Send OTP here
            // Validating...

            // Store in TempData/to verify later
            // Using Session or encrypted cookie is better, simple approach for MVP using TempData
            TempData["Reg_CCCD"] = cccd;
            TempData["Reg_Phone"] = phoneNumber;
            
            return RedirectToAction("VerifyOtp");
        }

        [HttpGet]
        public IActionResult VerifyOtp()
        {
            if (TempData.Peek("Reg_CCCD") == null) return RedirectToAction("Register");
            ViewBag.PhoneNumber = TempData.Peek("Reg_Phone");
            return View();
        }

        [HttpPost]
        public IActionResult VerifyOtp(string otp)
        {
            var cccd = TempData.Peek("Reg_CCCD")?.ToString();
            var phone = TempData.Peek("Reg_Phone")?.ToString();

            if (string.IsNullOrEmpty(cccd)) return RedirectToAction("Register");

            if (otp == "123456") // Mock OTP
            {
                // OTP verified, redirect to password creation
                TempData.Keep("Reg_CCCD");
                TempData.Keep("Reg_Phone");
                return RedirectToAction("CreatePassword");
            }
            else
            {
                ModelState.AddModelError("", "Mã OTP không chính xác");
                TempData.Keep("Reg_CCCD");
                TempData.Keep("Reg_Phone");
            }
            
            ViewBag.PhoneNumber = phone;
            return View();
        }

        [HttpGet]
        public IActionResult CreatePassword()
        {
            if (TempData.Peek("Reg_CCCD") == null) return RedirectToAction("Register");
            ViewBag.CCCD = TempData.Peek("Reg_CCCD");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreatePassword(string password, string confirmPassword)
        {
            var cccd = TempData["Reg_CCCD"]?.ToString();
            var phone = TempData["Reg_Phone"]?.ToString();

            if (string.IsNullOrEmpty(cccd)) return RedirectToAction("Register");

            // Validate password
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                ModelState.AddModelError("", "Mật khẩu phải có ít nhất 6 ký tự");
                TempData.Keep("Reg_CCCD");
                TempData.Keep("Reg_Phone");
                ViewBag.CCCD = cccd;
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu xác nhận không khớp");
                TempData.Keep("Reg_CCCD");
                TempData.Keep("Reg_Phone");
                ViewBag.CCCD = cccd;
                return View();
            }

            // Create user with provided password
            var user = new ApplicationUser 
            { 
                UserName = cccd, 
                Email = cccd + "@citizen.gov.vn",
                CCCD = cccd,
                PhoneNumber = phone,
                FullName = "Công dân (" + cccd + ")"
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Citizen");
                await _signInManager.SignInAsync(user, isPersistent: false);
                
                TempData["SuccessMessage"] = "Đăng ký thành công! Chào mừng bạn.";
                return RedirectToAction("Index", "Identity");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            TempData.Keep("Reg_CCCD");
            TempData.Keep("Reg_Phone");
            ViewBag.CCCD = cccd;
            return View();
        }


        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string cccd, string password, bool rememberMe)
        {
            // Login with Username (which is CCCD)
            var result = await _signInManager.PasswordSignInAsync(cccd, password, rememberMe, lockoutOnFailure: false);
            
            if (result.Succeeded)
            {
                return RedirectToAction("Index", "Home");
            }
            ModelState.AddModelError("", "Đăng nhập thất bại. Kiểm tra CCCD và mật khẩu.");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
            return View(user);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string fullName, string cccd, string phone, string address)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FullName = AdministrativeServices.Helpers.TextHelper.NormalizeName(fullName);
            user.CCCD = cccd;
            user.PhoneNumber = phone;
            user.Street = address; // Simple mapping for now
            // Future: Parse address or update View to send separate fields

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật thông tin.";
            }

            return RedirectToAction(nameof(Profile));
        }
    }
}
