using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // CS5, CS6 - digital menu. (Admin add/edit of items, AD3-AD5, will extend this.)
    public interface IMenuService
    {
        // Categories in display order, each with its items (available and sold out)
        Task<List<MenuCategory>> GetMenuAsync();
    }
}
