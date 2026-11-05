using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // CS5, CS6 - reads the digital menu for customers.
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
    }
}
