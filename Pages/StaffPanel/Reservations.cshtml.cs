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

        public ReservationsModel(IReservationService reservations)
        {
            _reservations = reservations;
        }

        [BindProperty(SupportsGet = true)]
        public DateTime? Date { get; set; }

        public List<Reservation> Reservations { get; set; } = new();

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            Date ??= DateTime.Today;
            Reservations = await _reservations.GetForDateAsync(Date.Value);
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
