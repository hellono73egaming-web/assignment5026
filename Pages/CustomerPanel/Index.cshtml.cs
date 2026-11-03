using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;

namespace WebApplication2.Pages.CustomerPanel
{
    // Customer home. Access restricted to the Customer role in Program.cs.
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public Customer? CurrentCustomer { get; set; }
        public List<Reservation> UpcomingReservations { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();

        public async Task OnGetAsync()
        {
            CurrentCustomer = await _userManager.GetUserAsync(User) as Customer;
            if (CurrentCustomer == null) return;

            var id = CurrentCustomer.Id;

            UpcomingReservations = await _db.Reservations
                .Include(r => r.RestaurantTable)
                .Where(r => r.CustomerId == id && r.StartTime >= DateTime.Now
                            && r.Status != ReservationStatus.Cancelled)
                .OrderBy(r => r.StartTime)
                .Take(5)
                .ToListAsync();

            Notifications = await _db.Notifications
                .Where(n => n.UserId == id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .ToListAsync();
        }
    }
}
