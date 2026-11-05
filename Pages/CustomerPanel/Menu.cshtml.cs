using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // CS5, CS6 (browse menu) and CS10, CS11 (place food & beverage orders).
    // The cart lives in the browser while browsing; on "Place Order" only item IDs and
    // quantities are sent. Prices, promotions and tax are recalculated on the server.
    public class MenuModel : PageModel
    {
        private readonly IMenuService _menu;
        private readonly IOrderService _orders;
        private readonly IReservationService _reservations;
        private readonly UserManager<ApplicationUser> _userManager;

        public MenuModel(IMenuService menu, IOrderService orders, IReservationService reservations,
                         UserManager<ApplicationUser> userManager)
        {
            _menu = menu;
            _orders = orders;
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

        public async Task<IActionResult> OnPostPlaceOrderAsync(string cartJson, OrderType orderType,
                                                               int? reservationId, string? notes)
        {
            var userId = _userManager.GetUserId(User)!;

            List<CartLine>? lines;
            try
            {
                lines = JsonSerializer.Deserialize<List<CartLine>>(cartJson ?? "[]",
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                lines = null;
            }

            // Basic sanity checks on what the browser sent
            lines = lines?
                .Where(l => l.MenuItemId > 0 && l.Quantity > 0)
                .Select(l => l with { Quantity = Math.Min(l.Quantity, CartService.MaxQuantityPerItem), Remarks = null })
                .ToList();

            if (lines == null || lines.Count == 0)
            {
                Error = "Your cart is empty.";
                return RedirectToPage();
            }

            if (orderType == OrderType.Takeaway) reservationId = null;

            var result = await _orders.PlaceOrderAsync(userId, lines, orderType, reservationId, notes);
            if (!result.Success)
            {
                Error = result.Error;
                return RedirectToPage();
            }

            PlacedOrderNumber = result.Value!.OrderNumber;
            PlacedOrderTotal = result.Value.Total.ToString("N2");
            return RedirectToPage();
        }
    }
}
