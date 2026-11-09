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
                    RestaurantName = "Artie's Restaurant",
                    Address = "Penang, Malaysia",
                    ReservationDurationMinutes = 120,
                    ServiceTaxPercent = 6
                });
            }
            else
            {
                // Databases created before the rebrand still say "RROMS Bistro" on receipts
                var setting = await db.RestaurantSettings.FirstAsync();
                if (setting.RestaurantName == "RROMS Bistro") setting.RestaurantName = "Artie's Restaurant";
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

            // 6. Menu (Italian menu from the frontend design).
            //    Runs on a new database, and once on an older database that still has
            //    the original sample items without photos (only if no orders exist yet).
            var hasPhotoMenu = await db.MenuItems.AnyAsync(m => m.ImageUrl != null);
            var hasOrders = await db.OrderItems.AnyAsync();
            if (!hasPhotoMenu && !hasOrders)
            {
                db.MenuItems.RemoveRange(await db.MenuItems.ToListAsync());
                db.MenuCategories.RemoveRange(await db.MenuCategories.ToListAsync());
                await db.SaveChangesAsync();

                static string Img(string id) => "https://images.unsplash.com/" + id + "?q=80&w=800&auto=format&fit=crop";

                var starters = new MenuCategory { Name = "Antipasti", Description = "Starters", DisplayOrder = 1 };
                var mains = new MenuCategory { Name = "Secondi & Pasta", Description = "Mains and pasta", DisplayOrder = 2 };
                var desserts = new MenuCategory { Name = "Dolci", Description = "Desserts", DisplayOrder = 3 };
                var drinks = new MenuCategory { Name = "Bevande", Description = "Beverages", DisplayOrder = 4 };

                starters.MenuItems.Add(new MenuItem { Name = "Zuppa di Mussels", Price = 16.00m, Description = "Prince Edward Island mussels cooked with garlic, herbs, and broth.", ImageUrl = Img("photo-1549472584-ecb8f58c1738"), Tags = "seafood,garlic" });
                starters.MenuItems.Add(new MenuItem { Name = "Fiori di Zucca Ripieni", Price = 14.00m, Description = "Zucchini flowers stuffed with melted dry ricotta cheese.", ImageUrl = Img("photo-1625938145744-e3805152e415"), Tags = "vegetarian,cheese" });
                starters.MenuItems.Add(new MenuItem { Name = "Fiori di Zucca Fritti", Price = 12.00m, Description = "Fried zucchini flowers, served golden and crispy with a side of marinara.", ImageUrl = Img("photo-1599305090598-fe179d501227"), Tags = "vegetarian,fried" });
                starters.MenuItems.Add(new MenuItem { Name = "Melanzane Grigliate", Price = 11.00m, Description = "Grilled eggplant with extra virgin olive oil, garlic, and fresh herbs.", ImageUrl = Img("photo-1572453800999-e8d2d1589b7c"), Tags = "vegetarian,grilled" });
                starters.MenuItems.Add(new MenuItem { Name = "Antipasto della Casa", Price = 18.00m, Description = "Assorted Italian cured meats, cheeses, olives, and marinated vegetables.", ImageUrl = Img("photo-1541529086526-db283c563270"), Tags = "meat,cheese,sharing" });
                starters.MenuItems.Add(new MenuItem { Name = "Calamari Ripieni", Price = 15.00m, Description = "Stuffed squid with breadcrumbs, herbs, and other secret ingredients.", ImageUrl = Img("photo-1599487488170-d11ec9c172f0"), Tags = "seafood" });
                mains.MenuItems.Add(new MenuItem { Name = "Quail alla Bucco", Price = 34.00m, Description = "Baby quail stuffed with fennel sausage. A house specialty.", ImageUrl = Img("photo-1518492104633-130d0cc84637"), Tags = "poultry,signature" });
                mains.MenuItems.Add(new MenuItem { Name = "Coniglio alla Bucco", Price = 38.00m, Description = "Rabbit prepared from a handwritten family cookbook.", ImageUrl = Img("photo-1580476262798-bddd9f4b7369"), Tags = "meat,signature" });
                mains.MenuItems.Add(new MenuItem { Name = "Branzino", Price = 36.00m, Description = "Mediterranean sea bass, typically served whole with lemon and fresh herbs.", ImageUrl = Img("photo-1535140728325-a4d3707eee61"), Tags = "seafood,fish" });
                mains.MenuItems.Add(new MenuItem { Name = "Ravioli", Price = 22.00m, Description = "Italian stuffed pasta, filled with ricotta or meat and served with our house sauce.", ImageUrl = Img("photo-1608897013039-887f21d8c804"), Tags = "pasta,cheese" });
                mains.MenuItems.Add(new MenuItem { Name = "Linguine alle Vongole", Price = 28.00m, Description = "Linguine with clams, garlic, parsley, and a splash of white wine.", ImageUrl = Img("photo-1563379926898-05f4575a45d8"), Tags = "pasta,seafood" });
                mains.MenuItems.Add(new MenuItem { Name = "Fettuccine Primavera", Price = 20.00m, Description = "Fettuccine tossed with fresh, seasonal vegetables in a light garlic and oil sauce.", ImageUrl = Img("photo-1645112411341-6c4fd023714a"), Tags = "pasta,vegetarian" });
                mains.MenuItems.Add(new MenuItem { Name = "Balsamic Grilled Veal Chop", Price = 42.00m, Description = "Thick-cut veal chop, grilled to perfection and finished with a sweet balsamic reduction.", ImageUrl = Img("photo-1544025162-d76694265947"), Tags = "meat,grilled" });
                mains.MenuItems.Add(new MenuItem { Name = "Spiedini", Price = 27.00m, Description = "Italian-style stuffed and rolled veal, skewered and grilled with a savory breadcrumb crust.", ImageUrl = Img("photo-1555939594-58d7cb561ad1"), Tags = "meat,grilled" });
                mains.MenuItems.Add(new MenuItem { Name = "Tonno alla Griglia", Price = 31.00m, Description = "Fresh grilled tuna steak seasoned with lemon, oregano, and a drizzle of olive oil.", ImageUrl = Img("photo-1519708227418-c8fd9a32b7a2"), Tags = "seafood,fish,grilled" });
                desserts.MenuItems.Add(new MenuItem { Name = "Classic Tiramisu", Price = 11.00m, Description = "Espresso-soaked ladyfingers layered with mascarpone cream and dusted with cocoa.", ImageUrl = Img("photo-1571115177098-24ec42ed204d"), Tags = "sweet,coffee" });
                desserts.MenuItems.Add(new MenuItem { Name = "Sicilian Cannoli", Price = 9.00m, Description = "Two crisp pastry shells filled with sweet ricotta and chocolate chips.", ImageUrl = Img("photo-1551799517-eb8de75c2c5a"), Tags = "sweet,chocolate" });
                desserts.MenuItems.Add(new MenuItem { Name = "Chocolate Lava Cake", Price = 10.00m, Description = "Warm dark chocolate cake with a molten center, served with vanilla bean ice cream.", ImageUrl = Img("photo-1551024506-0bccd828d307"), Tags = "sweet,chocolate" });
                drinks.MenuItems.Add(new MenuItem { Name = "Chianti Classico", Price = 15.00m, Description = "A robust, traditional Italian red wine to pair with your steak or pasta.", ImageUrl = Img("photo-1506377247377-2a5b3b417ebb"), Tags = "wine", ItemType = MenuItemType.Beverage, IsAvailable = false });
                drinks.MenuItems.Add(new MenuItem { Name = "Negroni", Price = 14.00m, Description = "A classic Italian cocktail made with gin, sweet vermouth, and Campari, garnished with orange.", ImageUrl = Img("photo-1556679343-c7306c1976bc"), Tags = "cocktail", ItemType = MenuItemType.Beverage });
                drinks.MenuItems.Add(new MenuItem { Name = "San Pellegrino", Price = 6.00m, Description = "Sparkling Italian mineral water served chilled with a slice of lemon.", ImageUrl = Img("photo-1527960656308-84b50bace286"), Tags = "cold,non-alcoholic", ItemType = MenuItemType.Beverage });
                drinks.MenuItems.Add(new MenuItem { Name = "Double Espresso", Price = 4.50m, Description = "A strong, rich shot of Italian espresso to finish off your meal.", ImageUrl = Img("photo-1514432324607-a09d9b4aefdd"), Tags = "hot,coffee,non-alcoholic", ItemType = MenuItemType.Beverage });

                db.MenuCategories.AddRange(starters, mains, desserts, drinks);
            }

            await db.SaveChangesAsync();

            // Orders that were paid online and then served before the auto-complete rule existed
            await db.Orders
                .Where(o => o.Status == OrderStatus.Served && o.Payment != null && o.Payment.Status == PaymentStatus.Paid)
                .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Completed));
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
