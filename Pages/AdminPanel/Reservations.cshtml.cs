using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // Admin e: monitor reservations (READ-ONLY).
    // Staff confirm / seat / cancel bookings on StaffPanel/Reservations;
    // the Administrator only reviews past, current and upcoming bookings here.
    public class ReservationsModel : PageModel
    {
        private readonly IReservationService _reservations;

        public ReservationsModel(IReservationService reservations)
        {
            _reservations = reservations;
        }

        // Filters from the GET form (?search=&date=&status=)
        [BindProperty(Name = "search", SupportsGet = true)] public string? SearchTerm { get; set; }
        [BindProperty(Name = "date", SupportsGet = true)] public DateTime? FilterDate { get; set; }
        [BindProperty(Name = "status", SupportsGet = true)] public string? FilterStatus { get; set; }

        public List<Reservation> Reservations { get; set; } = new();

        // Stat cards (counted before the status filter, so the cards always show the full picture)
        public int TotalReservations { get; set; }
        public int ConfirmedReservations { get; set; }
        public int PendingReservations { get; set; }
        public int CancelledReservations { get; set; }

        public async Task OnGetAsync()
        {
            var found = await _reservations.SearchAsync(FilterDate, SearchTerm);

            TotalReservations = found.Count;
            ConfirmedReservations = found.Count(r => r.Status == ReservationStatus.Confirmed);
            PendingReservations = found.Count(r => r.Status == ReservationStatus.Pending);
            CancelledReservations = found.Count(r => r.Status == ReservationStatus.Cancelled);

            Reservations = Enum.TryParse<ReservationStatus>(FilterStatus, out var status)
                ? found.Where(r => r.Status == status).ToList()
                : found;
        }
    }
}
