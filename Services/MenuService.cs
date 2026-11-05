using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // CS5, CS6 - reads the digital menu for customers.
    // AD3-AD5  - admin rules for the menu:
    //  - category and item names are unique
    //  - a category that still has items can't be deleted
    //  - price must be between RM 0.01 and RM 10,000
    //  - changing a price never changes old orders (OrderItem stores its own UnitPrice)
    public class MenuService : IMenuService
    {
        private readonly ApplicationDbContext _db;

        public MenuService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<MenuCategory>> GetMenuAsync()
        {
            var categories = await _db.MenuCategories
                .AsNoTracking()
                .Include(c => c.MenuItems)
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .ToListAsync();

            // Keep items in the order they were added
            foreach (var c in categories)
                c.MenuItems = c.MenuItems.OrderBy(i => i.Id).ToList();

            return categories;
        }

        // ------------------------------------------------------------
        // CATEGORIES
        // ------------------------------------------------------------
        public async Task<ServiceResult> SaveCategoryAsync(int id, string name, string? description)
        {
            name = name?.Trim() ?? string.Empty;
            if (name.Length < 2 || name.Length > 50)
                return ServiceResult.Fail("Category name must be 2-50 characters.");
            if (description?.Length > 250)
                return ServiceResult.Fail("Description can be at most 250 characters.");

            if (await _db.MenuCategories.AnyAsync(c => c.Id != id && c.Name.ToLower() == name.ToLower()))
                return ServiceResult.Fail($"A category called \"{name}\" already exists.");

            MenuCategory? category;
            if (id == 0)
            {
                var nextOrder = (await _db.MenuCategories.MaxAsync(c => (int?)c.DisplayOrder) ?? 0) + 1;
                category = new MenuCategory { DisplayOrder = nextOrder };
                _db.MenuCategories.Add(category);
            }
            else
            {
                category = await _db.MenuCategories.FindAsync(id);
                if (category == null) return ServiceResult.Fail("Category not found.");
            }

            category.Name = name;
            category.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteCategoryAsync(int id)
        {
            var category = await _db.MenuCategories.Include(c => c.MenuItems).FirstOrDefaultAsync(c => c.Id == id);
            if (category == null) return ServiceResult.Fail("Category not found.");

            if (category.MenuItems.Count > 0)
                return ServiceResult.Fail($"\"{category.Name}\" still has {category.MenuItems.Count} item(s). Move them to another category first.");

            _db.MenuCategories.Remove(category);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // ------------------------------------------------------------
        // ITEMS
        // ------------------------------------------------------------
        public async Task<ServiceResult> SaveItemAsync(int id, string name, string? description, decimal price,
                                                       int categoryId, string? imageUrl, bool isAvailable, MenuItemType itemType)
        {
            name = name?.Trim() ?? string.Empty;
            if (name.Length < 2 || name.Length > 100)
                return ServiceResult.Fail("Item name must be 2-100 characters.");
            if (description?.Length > 500)
                return ServiceResult.Fail("Description can be at most 500 characters.");
            if (price < 0.01m || price > 10000m)
                return ServiceResult.Fail("Price must be between RM 0.01 and RM 10,000.");
            if (!await _db.MenuCategories.AnyAsync(c => c.Id == categoryId))
                return ServiceResult.Fail("Please choose a category.");
            if (!string.IsNullOrWhiteSpace(imageUrl) && !Uri.IsWellFormedUriString(imageUrl.Trim(), UriKind.Absolute))
                return ServiceResult.Fail("Image URL must be a full link starting with http:// or https://.");

            if (await _db.MenuItems.AnyAsync(m => m.Id != id && m.Name.ToLower() == name.ToLower()))
                return ServiceResult.Fail($"A menu item called \"{name}\" already exists.");

            MenuItem? item;
            if (id == 0)
            {
                item = new MenuItem();
                _db.MenuItems.Add(item);
            }
            else
            {
                item = await _db.MenuItems.FindAsync(id);
                if (item == null) return ServiceResult.Fail("Menu item not found.");
            }

            item.Name = name;
            item.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            item.Price = Math.Round(price, 2);
            item.MenuCategoryId = categoryId;
            item.ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
            item.IsAvailable = isAvailable;
            item.ItemType = itemType;

            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> SetItemAvailabilityAsync(int id, bool isAvailable)
        {
            var item = await _db.MenuItems.FindAsync(id);
            if (item == null) return ServiceResult.Fail("Menu item not found.");

            item.IsAvailable = isAvailable;
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }
    }
}
