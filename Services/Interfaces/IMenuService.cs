using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // CS5, CS6 - digital menu for customers.
    // AD3, AD4, AD5 - admin manages categories, items, prices and availability.
    public interface IMenuService
    {
        // Categories in display order, each with its items (available and sold out)
        Task<List<MenuCategory>> GetMenuAsync();

        // Categories (AD3)
        Task<ServiceResult> SaveCategoryAsync(int id, string name, string? description);   // id 0 = add
        Task<ServiceResult> DeleteCategoryAsync(int id);

        // Items, pricing, availability (AD4, AD5)
        Task<ServiceResult> SaveItemAsync(int id, string name, string? description, decimal price,
                                          int categoryId, string? imageUrl, bool isAvailable, MenuItemType itemType);   // id 0 = add
        Task<ServiceResult> SetItemAvailabilityAsync(int id, bool isAvailable);
    }
}
