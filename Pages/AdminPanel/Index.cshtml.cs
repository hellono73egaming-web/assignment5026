using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // Presentation tier only: asks the service layer for data, never queries the database.
    // Access restricted to the Administrator role in Program.cs.
    public class IndexModel : PageModel
    {
        private readonly IDashboardService _dashboard;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(IDashboardService dashboard, UserManager<ApplicationUser> userManager)
        {
            _dashboard = dashboard;
            _userManager = userManager;
        }

        public string AdminName { get; set; } = string.Empty;
        public int CustomerCount { get; set; }
        public int StaffCount { get; set; }
        public int TodayReservations { get; set; }
        public int ActiveOrders { get; set; }
        public decimal TodaySales { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            AdminName = user?.FullName ?? "Admin";

            var stats = await _dashboard.GetAdminStatsAsync();
            CustomerCount = stats.CustomerCount;
            StaffCount = stats.StaffCount;
            TodayReservations = stats.TodayReservations;
            ActiveOrders = stats.ActiveOrders;
            TodaySales = stats.TodaySales;
        }
    }
}
