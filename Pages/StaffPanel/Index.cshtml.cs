using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.StaffPanel
{
    // Staff dashboard: today's numbers and what needs doing next.
    // Presentation tier only: asks the service layer for data, never queries the database.
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
        public StaffDashboardData Data { get; set; } = null!;

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            StaffName = (user?.FullName ?? "Staff").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Staff";
            Position = (user as Staff)?.Position ?? user?.RoleName ?? "";

            Data = await _dashboard.GetStaffDashboardAsync();
        }

        // ---- Helpers used by the page ----
        public static string ReservationCss(ReservationStatus s) => s switch
        {
            ReservationStatus.Pending => "badge-pending",
            ReservationStatus.Confirmed => "badge-confirmed",
            ReservationStatus.Seated => "badge-seated",
            _ => "badge-muted"
        };

        public static string OrderCss(OrderStatus s) => s == OrderStatus.ReadyToServe ? "badge-seated" : "badge-pending";

        public static string TableOf(Order o) =>
            o.Reservation?.RestaurantTable?.TableNumber ?? (o.OrderType == OrderType.Takeaway ? "Takeaway" : "Dine-in");
    }
}
