using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Pages.StaffPanel
{
    // Staff home. Access restricted to Staff (and Admin) in Program.cs.
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
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

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            TodayReservations = await _db.Reservations
                .CountAsync(r => r.StartTime >= today && r.StartTime < tomorrow);
            PendingReservations = await _db.Reservations
                .CountAsync(r => r.Status == ReservationStatus.Pending);
            PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
            PreparingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Preparing);
        }
    }
}
