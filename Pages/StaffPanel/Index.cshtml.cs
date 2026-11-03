using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.StaffPanel
{
    // Presentation tier only: asks the service layer for data, never queries the database.
    // Access restricted to Staff (and Admin) in Program.cs.
    public class IndexModel : PageModel
    {
        private readonly IDashboardService _dashboard;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(IDashboardService dashboard, UserManager<ApplicationUser> userManager)
        {
            _dashboard = dashboard;
            _userManager = userManager;
        }

        public string StaffName { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public int PendingReservations { get; set; }
        public int TodayReservations { get; set; }
        public int PendingOrders { get; set; }
        public int PreparingOrders { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            StaffName = user?.FullName ?? "Staff";
            Position = (user as Staff)?.Position ?? user?.RoleName ?? "";

            var stats = await _dashboard.GetStaffStatsAsync();
            TodayReservations = stats.TodayReservations;
            PendingReservations = stats.PendingReservations;
            PendingOrders = stats.PendingOrders;
            PreparingOrders = stats.PreparingOrders;
        }
    }
}
