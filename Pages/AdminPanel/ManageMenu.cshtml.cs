using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // AD3 (categories), AD4 (items), AD5 (pricing and availability).
    // Admin-only (AdminPanel folder is locked to the Administrator role in Program.cs).
    public class ManageMenuModel : PageModel
    {
        private readonly IMenuService _menu;
        private readonly IImageStorageService _images;

        public ManageMenuModel(IMenuService menu, IImageStorageService images)
        {
            _menu = menu;
            _images = images;
        }

        public List<MenuCategory> Categories { get; set; } = new();
        public List<MenuItem> Items { get; set; } = new();
        public string Tab { get; set; } = "items";

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }
        [TempData] public string? ActiveTab { get; set; }

        public class ItemInput
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public decimal Price { get; set; }
            public int CategoryId { get; set; }
            public string? ImageUrl { get; set; }
            public bool IsAvailable { get; set; }
            public MenuItemType ItemType { get; set; }
        }

        public class CategoryInput
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
        }

        public async Task OnGetAsync()
        {
            Categories = await _menu.GetMenuAsync();
            Items = Categories.SelectMany(c => c.MenuItems.Select(i => { i.MenuCategory = c; return i; })).ToList();
            Tab = ActiveTab ?? "items";
        }

        // ---------- Items ----------
        // An uploaded photo wins over the URL box. If the save fails, the new file is removed again.
        public async Task<IActionResult> OnPostSaveItemAsync(ItemInput item, IFormFile? imageFile)
        {
            string? uploaded = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                var upload = await _images.SaveAsync(imageFile, "menu");
                if (!upload.Success) return Done(upload, "", "items");
                uploaded = upload.Value;
                item.ImageUrl = uploaded;
            }

            var result = await _menu.SaveItemAsync(item.Id, item.Name, item.Description, item.Price,
                                                   item.CategoryId, item.ImageUrl, item.IsAvailable, item.ItemType);
            if (!result.Success) _images.Delete(uploaded);
            return Done(result, item.Id == 0 ? $"\"{item.Name}\" added to the menu." : $"\"{item.Name}\" updated.", "items");
        }

        public async Task<IActionResult> OnPostToggleItemAsync(int id, bool available)
        {
            var result = await _menu.SetItemAvailabilityAsync(id, available);
            return Done(result, available ? "Item marked as available." : "Item marked as sold out.", "items");
        }

        // ---------- Categories ----------
        public async Task<IActionResult> OnPostSaveCategoryAsync(CategoryInput category)
        {
            var result = await _menu.SaveCategoryAsync(category.Id, category.Name, category.Description);
            return Done(result, category.Id == 0 ? $"Category \"{category.Name}\" added." : $"Category \"{category.Name}\" updated.", "categories");
        }

        public async Task<IActionResult> OnPostDeleteCategoryAsync(int id)
        {
            var result = await _menu.DeleteCategoryAsync(id);
            return Done(result, "Category deleted.", "categories");
        }

        private IActionResult Done(ServiceResult result, string successMessage, string tab)
        {
            if (result.Success) Message = successMessage;
            else Error = result.Error;
            ActiveTab = tab;
            return RedirectToPage();
        }
    }
}
