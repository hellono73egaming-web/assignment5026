using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // Presentation tier only: asks the service layer for data, never queries the database.
    // Access restricted to the Customer role in Program.cs.
    public class IndexModel : PageModel
    {
        private readonly IDashboardService _dashboard;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(IDashboardService dashboard, UserManager<ApplicationUser> userManager)
        {
            _dashboard = dashboard;
            _userManager = userManager;
        }

        public Customer? CurrentCustomer { get; set; }
        public List<Reservation> UpcomingReservations { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return;

            var data = await _dashboard.GetCustomerDashboardAsync(userId);
            CurrentCustomer = data.Customer;
            UpcomingReservations = data.UpcomingReservations;
            Notifications = data.Notifications;
        }
    }
}
