using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.StaffPanel
{
    // RS4-RS8, RS15-RS17: staff view of one day's bookings.
    // Confirm / Check-in / Complete / No-show / Cancel all go through ReservationService,
    // which only allows valid status changes and notifies the customer.
    public class ReservationsModel : PageModel
    {
        private readonly IReservationService _reservations;
        private readonly IRestaurantService _restaurant;

        public ReservationsModel(IReservationService reservations, IRestaurantService restaurant)
        {
            _reservations = reservations;
            _restaurant = restaurant;
        }

        [BindProperty(SupportsGet = true)]
        public DateTime? Date { get; set; }

        public List<Reservation> Reservations { get; set; } = new();
        public List<RestaurantTable> Tables { get; set; } = new();

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            Date ??= DateTime.Today;
            Reservations = await _reservations.GetForDateAsync(Date.Value);
            Tables = (await _restaurant.GetTablesAsync()).Where(t => t.IsActive).ToList();
        }

        // RS6 - staff modify a booking (time / table / party size).
        // ModifyAsync re-checks opening hours, table capacity and clashes, then notifies the customer.
        public async Task<IActionResult> OnPostModifyAsync(int id, string code, DateTime newDate, TimeSpan newTime,
                                                           int tableId, int partySize, string? requests)
        {
            var start = newDate.Date + newTime;
            var result = await _reservations.ModifyAsync(id, start, partySize, requests, tableId);

            if (result.Success) Message = $"{code} updated to {start:h:mm tt}, Table {result.Value!.RestaurantTable?.TableNumber}, {partySize} pax.";
            else Error = result.Error;

            return RedirectToPage(new { date = Date?.ToString("yyyy-MM-dd") });
        }

        public async Task<IActionResult> OnPostStatusAsync(int id, ReservationStatus status, string code)
        {
            var result = await _reservations.ChangeStatusAsync(id, status);

            if (result.Success) Message = $"{code} marked as {Label(status)}.";
            else Error = result.Error;

            return RedirectToPage(new { date = Date?.ToString("yyyy-MM-dd") });
        }

        // ---- Helpers used by the page ----
        public static string Label(ReservationStatus s) => s == ReservationStatus.NoShow ? "No-show" : s.ToString();

        public static string Css(ReservationStatus s) => s == ReservationStatus.NoShow ? "noshow" : s.ToString().ToLower();
    }
}
