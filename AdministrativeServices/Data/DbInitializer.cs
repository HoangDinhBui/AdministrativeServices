using AdministrativeServices.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AdministrativeServices.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Seed Roles
            string[] roles = { "Citizen", "Official", "Chairman", "Admin" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Seed Admin User
            if (await userManager.FindByEmailAsync("admin@admin.com") == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin@admin.com",
                    Email = "admin@admin.com",
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(admin, "Admin@123");
                await userManager.AddToRoleAsync(admin, "Admin");
            }
            
            // Seed Official User
            if (await userManager.FindByEmailAsync("official@gov.vn") == null)
            {
                var official = new ApplicationUser
                {
                    UserName = "official@gov.vn",
                    Email = "official@gov.vn",
                    FullName = "Cán Bộ Tiếp Nhận",
                    Department = "Bộ phận Một cửa",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(official, "Official@123");
                await userManager.AddToRoleAsync(official, "Official");
            }

            // Seed Citizens (National Database Simulation)
            var citizens = new List<Citizen>
            {
                new Citizen
                {
                    CCCD = "001099000001",
                    FullName = "Nguyễn Văn A",
                    DateOfBirth = new DateTime(1999, 1, 1),
                    Gender = "Nam",
                    PlaceOfBirth = "Hà Nội",
                    Hometown = "Nam Định",
                    PermanentAddress = "Số 1, Đại Cồ Việt, Hai Bà Trưng, Hà Nội",
                    Ethnicity = "Kinh",
                    Nationality = "Việt Nam",
                    MaritalStatus = "Chưa kết hôn"
                },
                new Citizen
                {
                    CCCD = "001099000002",
                    FullName = "Trần Thị B",
                    DateOfBirth = new DateTime(2000, 5, 15),
                    Gender = "Nữ",
                    PlaceOfBirth = "Đà Nẵng",
                    Hometown = "Quảng Nam",
                    PermanentAddress = "Số 10, Lê Duẩn, Hải Châu, Đà Nẵng",
                    Ethnicity = "Kinh",
                    Nationality = "Việt Nam",
                    MaritalStatus = "Chưa kết hôn"
                }
            };

            foreach (var c in citizens)
            {
                if (!await context.Citizens.AnyAsync(x => x.CCCD == c.CCCD))
                {
                    context.Citizens.Add(c);
                }
            }
            await context.SaveChangesAsync();
        }
    }
}
