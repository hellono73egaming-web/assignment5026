using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // AD2 / Admin b - dining areas, tables and opening hours.
    // Admin-only (AdminPanel folder is locked to the Administrator role in Program.cs).
    public class ManageRestaurantModel : PageModel
    {
        private readonly IRestaurantService _restaurant;

        public ManageRestaurantModel(IRestaurantService restaurant)
        {
            _restaurant = restaurant;
        }

        public List<DiningArea> Areas { get; set; } = new();
        public List<RestaurantTable> Tables { get; set; } = new();
        public List<OperatingHour> Hours { get; set; } = new();
        public string Tab { get; set; } = "areas";

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }
        [TempData] public string? ActiveTab { get; set; }   // re-opens the tab the admin was on

        // Form shapes (one per form, so each handler only binds its own fields)
        public class AreaInput
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? ImageUrl { get; set; }
        }

        public class TableInput
        {
            public int Id { get; set; }
            public string TableNumber { get; set; } = string.Empty;
            public int Capacity { get; set; } = 2;
            public int DiningAreaId { get; set; }
            public bool IsActive { get; set; }
        }

        public class HourInput
        {
            public DayOfWeek Day { get; set; }
            public TimeSpan OpenTime { get; set; }
            public TimeSpan CloseTime { get; set; }
            public bool IsClosed { get; set; }
        }

        public async Task OnGetAsync()
        {
            Areas = await _restaurant.GetAreasAsync();
            Tables = await _restaurant.GetTablesAsync();
            Hours = await _restaurant.GetOperatingHoursAsync();
            Tab = ActiveTab ?? "areas";
        }

        // ---------- Dining areas ----------
        public async Task<IActionResult> OnPostSaveAreaAsync(AreaInput area)
        {
            var result = await _restaurant.SaveAreaAsync(area.Id, area.Name, area.Description, area.ImageUrl);
            return Done(result, area.Id == 0 ? $"Dining area \"{area.Name}\" added." : $"Dining area \"{area.Name}\" updated.", "areas");
        }

        public async Task<IActionResult> OnPostDeleteAreaAsync(int id)
        {
            var result = await _restaurant.DeleteAreaAsync(id);
            return Done(result, "Dining area deleted.", "areas");
        }

        // ---------- Tables ----------
        public async Task<IActionResult> OnPostSaveTableAsync(TableInput table)
        {
            var result = await _restaurant.SaveTableAsync(table.Id, table.TableNumber, table.Capacity, table.DiningAreaId, table.IsActive);
            return Done(result, table.Id == 0 ? $"Table {table.TableNumber.ToUpper()} added." : $"Table {table.TableNumber.ToUpper()} updated.", "tables");
        }

        public async Task<IActionResult> OnPostToggleTableAsync(int id, bool active)
        {
            var result = await _restaurant.SetTableActiveAsync(id, active);
            return Done(result, active ? "Table activated." : "Table deactivated.", "tables");
        }

        // ---------- Opening hours ----------
        public async Task<IActionResult> OnPostSaveHoursAsync(List<HourInput> hours)
        {
            var result = await _restaurant.SaveOperatingHoursAsync(hours.Select(h => new OperatingHour
            {
                Day = h.Day,
                OpenTime = h.OpenTime,
                CloseTime = h.CloseTime,
                IsClosed = h.IsClosed
            }));
            return Done(result, "Opening hours saved.", "hours");
        }

        // Post/Redirect/Get with a success or error message
        private IActionResult Done(Services.ServiceResult result, string successMessage, string tab)
        {
            if (result.Success) Message = successMessage;
            else Error = result.Error;
            ActiveTab = tab;
            return RedirectToPage();
        }
    }
}
