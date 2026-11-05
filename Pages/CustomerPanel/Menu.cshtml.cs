using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // CS5, CS6 (browse menu) and CS10, CS11 (add food & beverages to the cart).
    // The cart lives in the browser while browsing; "Checkout" sends only item IDs and
    // quantities to the Checkout page, which recalculates prices on the server.
    public class MenuModel : PageModel
    {
        private readonly IMenuService _menu;
        private readonly IReservationService _reservations;
        private readonly UserManager<ApplicationUser> _userManager;

        public MenuModel(IMenuService menu, IReservationService reservations,
                         UserManager<ApplicationUser> userManager)
        {
            _menu = menu;
            _reservations = reservations;
            _userManager = userManager;
        }

        public List<MenuCategory> Categories { get; set; } = new();
        public List<Reservation> ActiveReservations { get; set; } = new();
        public decimal TaxPercent { get; set; }

        [TempData] public string? PlacedOrderNumber { get; set; }
        [TempData] public string? PlacedOrderTotal { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            Categories = await _menu.GetMenuAsync();
            TaxPercent = (await _reservations.GetSettingsAsync()).ServiceTaxPercent;

            // Dine-in orders can be linked to one of the customer's upcoming bookings
            var userId = _userManager.GetUserId(User)!;
            ActiveReservations = (await _reservations.GetForCustomerAsync(userId))
                .Where(r => (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed
                             || r.Status == ReservationStatus.Seated) && r.EndTime >= DateTime.Now)
                .OrderBy(r => r.StartTime)
                .ToList();
        }

    }
}
