using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;

namespace WebApplication2.Data
{
    // Runs once at startup: creates the 3 roles, a default admin and some sample data.
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await db.Database.MigrateAsync();

            // 1. Roles
            foreach (var role in new[] { Roles.Admin, Roles.Staff, Roles.Customer })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // 2. Default accounts - one of each subclass (change passwords before demo!)
            await CreateUser(userManager, new Admin
            {
                UserName = "admin@rroms.com", Email = "admin@rroms.com", FullName = "System Administrator"
            }, "Admin@123");

            await CreateUser(userManager, new Staff
            {
                UserName = "staff@rroms.com", Email = "staff@rroms.com", FullName = "Default Staff",
                EmployeeId = "EMP001", Position = "Waiter"
            }, "Staff@123");

            await CreateUser(userManager, new Customer
            {
                UserName = "customer@rroms.com", Email = "customer@rroms.com", FullName = "Demo Customer"
            }, "Customer@123");

            // 3. Restaurant settings
            if (!await db.RestaurantSettings.AnyAsync())
            {
                db.RestaurantSettings.Add(new RestaurantSetting
                {
                    RestaurantName = "RROMS Bistro",
                    Address = "Penang, Malaysia",
                    ReservationDurationMinutes = 120,
                    ServiceTaxPercent = 6
                });
            }

            // 4. Operating hours: 11:00 - 22:00 every day
            if (!await db.OperatingHours.AnyAsync())
            {
                foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
                {
                    db.OperatingHours.Add(new OperatingHour
                    {
                        Day = day,
                        OpenTime = new TimeSpan(11, 0, 0),
                        CloseTime = new TimeSpan(22, 0, 0)
                    });
                }
            }

            // 5. Dining areas + tables
            if (!await db.DiningAreas.AnyAsync())
            {
                var main = new DiningArea { Name = "Main Hall" };
                var outdoor = new DiningArea { Name = "Outdoor" };
                main.Tables.Add(new RestaurantTable { TableNumber = "M1", Capacity = 2 });
                main.Tables.Add(new RestaurantTable { TableNumber = "M2", Capacity = 4 });
                main.Tables.Add(new RestaurantTable { TableNumber = "M3", Capacity = 4 });
                main.Tables.Add(new RestaurantTable { TableNumber = "M4", Capacity = 6 });
                outdoor.Tables.Add(new RestaurantTable { TableNumber = "O1", Capacity = 2 });
                outdoor.Tables.Add(new RestaurantTable { TableNumber = "O2", Capacity = 4 });
                db.DiningAreas.AddRange(main, outdoor);
            }

            // 6. Menu
            if (!await db.MenuCategories.AnyAsync())
            {
                var mains = new MenuCategory { Name = "Mains", DisplayOrder = 1 };
                var drinks = new MenuCategory { Name = "Drinks", DisplayOrder = 2 };
                var desserts = new MenuCategory { Name = "Desserts", DisplayOrder = 3 };

                mains.MenuItems.Add(new MenuItem { Name = "Nasi Lemak Ayam", Price = 15.90m, Description = "Coconut rice with fried chicken and sambal", Tags = "spicy,chicken,local" });
                mains.MenuItems.Add(new MenuItem { Name = "Char Kway Teow", Price = 13.50m, Description = "Penang-style fried flat noodles", Tags = "seafood,local,noodles" });
                mains.MenuItems.Add(new MenuItem { Name = "Grilled Salmon", Price = 32.00m, Description = "With mashed potato and greens", Tags = "seafood,western" });
                mains.MenuItems.Add(new MenuItem { Name = "Vegetable Curry", Price = 12.00m, Description = "Mixed vegetables in mild curry", Tags = "vegetarian,local" });
                drinks.MenuItems.Add(new MenuItem { Name = "Teh Tarik", ItemType = MenuItemType.Beverage, Price = 4.50m, Tags = "hot,local" });
                drinks.MenuItems.Add(new MenuItem { Name = "Iced Lemon Tea", ItemType = MenuItemType.Beverage, Price = 5.00m, Tags = "cold" });
                desserts.MenuItems.Add(new MenuItem { Name = "Cendol", Price = 7.50m, Tags = "cold,local,sweet" });

                db.MenuCategories.AddRange(mains, drinks, desserts);
            }

            await db.SaveChangesAsync();
        }

        // Accepts ANY subclass of ApplicationUser (polymorphism) and
        // assigns the Identity role that the subclass reports via RoleName.
        private static async Task CreateUser(UserManager<ApplicationUser> userManager,
            ApplicationUser user, string password)
        {
            if (await userManager.FindByEmailAsync(user.Email!) != null) return;

            user.EmailConfirmed = true;
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(user, user.RoleName);
        }
    }
}
