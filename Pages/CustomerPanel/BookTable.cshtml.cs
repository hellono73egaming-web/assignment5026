using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // CS7 (make) and CS8 (modify, via ?editId=) - customer picks date/time/guests,
    // the system shows ONLY tables that are free (Intermediate #1), customer picks one.
    public class BookTableModel : PageModel
    {
        private readonly IReservationService _reservations;
        private readonly UserManager<ApplicationUser> _userManager;

        public BookTableModel(IReservationService reservations, UserManager<ApplicationUser> userManager)
        {
            _reservations = reservations;
            _userManager = userManager;
        }

        // Search values (kept in the URL so "Check availability" is a normal GET)
        [BindProperty(SupportsGet = true)] public DateTime? Date { get; set; }
        [BindProperty(SupportsGet = true)] public TimeSpan? Time { get; set; }
        [BindProperty(SupportsGet = true)] public int Guests { get; set; } = 2;
        [BindProperty(SupportsGet = true)] public int? EditId { get; set; }

        [BindProperty] public int TableId { get; set; }
        [BindProperty] public string? SpecialRequests { get; set; }

        public List<RestaurantTable> AvailableTables { get; set; } = new();
        public bool Searched { get; set; }
        public string? Error { get; set; }
        public int DurationMinutes { get; set; }
        public int? CurrentTableId { get; set; }   // when modifying, highlights the existing table

        // Confirmation modal (filled after a successful booking)
        [TempData] public string? BookedCode { get; set; }
        [TempData] public string? BookedTable { get; set; }
        [TempData] public string? BookedWhen { get; set; }
        [TempData] public string? BookedStatus { get; set; }
        [TempData] public bool BookedWasEdit { get; set; }

        public bool IsEdit => EditId != null;

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = _userManager.GetUserId(User)!;

            // Modifying: pre-fill the form from the existing booking (only your own)
            if (IsEdit && Date == null)
            {
                var existing = (await _reservations.GetForCustomerAsync(userId)).FirstOrDefault(r => r.Id == EditId);
                if (existing == null) return RedirectToPage("/CustomerPanel/MyReservations");

                Date = existing.StartTime.Date;
                Time = existing.StartTime.TimeOfDay;
                Guests = existing.PartySize;
                SpecialRequests = existing.SpecialRequests;
                CurrentTableId = existing.RestaurantTableId;
            }

            Date ??= DateTime.Today;
            Time ??= new TimeSpan(19, 0, 0);

            if (Request.Query.ContainsKey("date") || IsEdit)
                await SearchAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = _userManager.GetUserId(User)!;

            if (Date == null || Time == null)
            {
                Error = "Please choose a date and time.";
                return Page();
            }
            if (TableId == 0)
            {
                Error = "Please select a table.";
                await SearchAsync();
                return Page();
            }

            var start = Date.Value.Date + Time.Value;

            // All rules (opening hours, party size, double-booking) are checked in the service
            var result = IsEdit
                ? await _reservations.ModifyAsync(EditId!.Value, start, Guests, SpecialRequests, TableId, userId)
                : await _reservations.CreateAsync(userId, start, Guests, SpecialRequests, TableId);

            if (!result.Success)
            {
                Error = result.Error;
                await SearchAsync();
                return Page();
            }

            var r = result.Value!;
            BookedCode = r.ReservationCode;
            BookedTable = $"{r.RestaurantTable?.TableNumber} ({r.RestaurantTable?.DiningArea?.Name ?? "Table"})";
            BookedWhen = $"{r.StartTime:ddd, d MMM yyyy} at {r.StartTime:h:mm tt}";
            BookedStatus = r.Status.ToString();
            BookedWasEdit = IsEdit;

            // Post/Redirect/Get - refreshing won't book twice
            return RedirectToPage();
        }

        private async Task SearchAsync()
        {
            Searched = true;
            var settings = await _reservations.GetSettingsAsync();
            DurationMinutes = settings.ReservationDurationMinutes;

            if (Date == null || Time == null) { Error ??= "Please choose a date and time."; return; }
            if (Guests < 1 || Guests > 20) { Error ??= "Guests must be between 1 and 20."; return; }

            var start = Date.Value.Date + Time.Value;
            var slotError = await _reservations.ValidateTimeSlotAsync(start, start.AddMinutes(DurationMinutes));
            if (slotError != null) { Error ??= slotError; return; }

            AvailableTables = await _reservations.GetAvailableTablesAsync(start, Guests, EditId);
            if (AvailableTables.Count == 0)
                Error ??= $"Sorry, no table for {Guests} is free at {start:h:mm tt} on {start:ddd d MMM}. Try another time.";
        }
    }
}
