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

            // Seed Chairman User
            if (await userManager.FindByEmailAsync("chairman@gov.vn") == null)
            {
                var chairman = new ApplicationUser
                {
                    UserName = "chairman@gov.vn",
                    Email = "chairman@gov.vn",
                    FullName = "Chủ Tịch Ủy Ban",
                    Position = "Chủ tịch",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(chairman, "Chairman@123");
                await userManager.AddToRoleAsync(chairman, "Chairman");
            }

            // Seed Another Leader User
            if (await userManager.FindByEmailAsync("leader@gov.vn") == null)
            {
                var leader = new ApplicationUser
                {
                    UserName = "leader@gov.vn",
                    Email = "leader@gov.vn",
                    FullName = "Phó Chủ Tịch",
                    Position = "Phó Chủ tịch",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(leader, "Leader@123");
                await userManager.AddToRoleAsync(leader, "Chairman");
            }

            // Seed Citizens (National Database Simulation)
            var citizens = new List<Citizen>
            {
                new Citizen
                {
                    CCCD = "001099000001",
                    FullName = "Nguyễn Văn An",
                    DateOfBirth = new DateTime(1990, 1, 15),
                    Gender = "Nam",
                    PlaceOfBirth = "Hà Nội",
                    Hometown = "Nam Định",
                    PermanentAddress = "Số 1, Đại Cồ Việt, Hai Bà Trưng, Hà Nội",
                    Ethnicity = "Kinh",
                    Nationality = "Việt Nam",
                    MaritalStatus = "Đã kết hôn"
                },
                new Citizen
                {
                    CCCD = "001099000002",
                    FullName = "Trần Thị Bình",
                    DateOfBirth = new DateTime(1992, 5, 20),
                    Gender = "Nữ",
                    PlaceOfBirth = "Đà Nẵng",
                    Hometown = "Quảng Nam",
                    PermanentAddress = "Số 1, Đại Cồ Việt, Hai Bà Trưng, Hà Nội",
                    Ethnicity = "Kinh",
                    Nationality = "Việt Nam",
                    MaritalStatus = "Đã kết hôn"
                },
                new Citizen
                {
                    CCCD = "001099000003",
                    FullName = "Lê Hoàng Cường",
                    DateOfBirth = new DateTime(1988, 8, 10),
                    Gender = "Nam",
                    PlaceOfBirth = "TP Hồ Chí Minh",
                    Hometown = "Bình Dương",
                    PermanentAddress = "Số 50, Nguyễn Huệ, Quận 1, TP Hồ Chí Minh",
                    Ethnicity = "Kinh",
                    Nationality = "Việt Nam",
                    MaritalStatus = "Chưa kết hôn"
                },
                new Citizen
                {
                    CCCD = "001099000004",
                    FullName = "Phạm Thị Dung",
                    DateOfBirth = new DateTime(1995, 3, 25),
                    Gender = "Nữ",
                    PlaceOfBirth = "Hải Phòng",
                    Hometown = "Hải Phòng",
                    PermanentAddress = "Số 25, Lạch Tray, Ngô Quyền, Hải Phòng",
                    Ethnicity = "Kinh",
                    Nationality = "Việt Nam",
                    MaritalStatus = "Chưa kết hôn"
                },
                new Citizen
                {
                    CCCD = "001099000005",
                    FullName = "Hoàng Minh Em",
                    DateOfBirth = new DateTime(2000, 12, 1),
                    Gender = "Nam",
                    PlaceOfBirth = "Cần Thơ",
                    Hometown = "Cần Thơ",
                    PermanentAddress = "Số 100, Đường 3/2, Ninh Kiều, Cần Thơ",
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
