using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // CS9 (cancel), CS12 (history), CS15 (status). Customers only see THEIR OWN bookings.
    public class MyReservationsModel : PageModel
    {
        private readonly IReservationService _reservations;
        private readonly UserManager<ApplicationUser> _userManager;

        public MyReservationsModel(IReservationService reservations, UserManager<ApplicationUser> userManager)
        {
            _reservations = reservations;
            _userManager = userManager;
        }

        public List<Reservation> Reservations { get; set; } = new();

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            Reservations = await _reservations.GetForCustomerAsync(userId);
        }

        public async Task<IActionResult> OnPostCancelAsync(int id)
        {
            // Passing the customer ID makes the service refuse other people's bookings
            var userId = _userManager.GetUserId(User)!;
            var result = await _reservations.CancelAsync(id, userId);

            if (result.Success) Message = "Your reservation has been cancelled and the table released.";
            else Error = result.Error;

            return RedirectToPage();
        }

        // ---- Helpers used by the page ----
        public static bool IsActive(Reservation r) =>
            r.Status is ReservationStatus.Pending or ReservationStatus.Confirmed or ReservationStatus.Seated;

        public static bool IsUpcoming(Reservation r) => IsActive(r) && r.EndTime >= DateTime.Now;

        public static bool CanChange(Reservation r) =>
            (r.Status is ReservationStatus.Pending or ReservationStatus.Confirmed) && r.StartTime > DateTime.Now;

        public static string StatusCss(ReservationStatus s) => s switch
        {
            ReservationStatus.NoShow => "noshow",
            _ => s.ToString().ToLower()
        };

        public static string StatusLabel(ReservationStatus s) => s == ReservationStatus.NoShow ? "No-show" : s.ToString();
    }
}
