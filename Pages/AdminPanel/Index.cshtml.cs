using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Pages.AdminPanel
{
    // Admin home. Access restricted to the Administrator role in Program.cs.
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
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

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            CustomerCount = await _db.Customers.CountAsync();
            StaffCount = await _db.StaffMembers.CountAsync();
            TodayReservations = await _db.Reservations
                .CountAsync(r => r.StartTime >= today && r.StartTime < tomorrow);
            ActiveOrders = await _db.Orders
                .CountAsync(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
            TodaySales = await _db.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= today && p.PaidAt < tomorrow)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
        }
    }
}
