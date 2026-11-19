using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // CS17 / Customer 3h: rate a completed dining experience (one review per visit).
    // The rules (1-5 stars, completed visits only, no double rating) live in FeedbackService.
    public class FeedbackModel : PageModel
    {
        private readonly IFeedbackService _feedback;
        private readonly IReservationService _reservations;
        private readonly IOrderService _orders;
        private readonly UserManager<ApplicationUser> _userManager;

        public FeedbackModel(IFeedbackService feedback, IReservationService reservations,
                             IOrderService orders, UserManager<ApplicationUser> userManager)
        {
            _feedback = feedback;
            _reservations = reservations;
            _orders = orders;
            _userManager = userManager;
        }

        public List<Reservation> Visits { get; set; } = new();
        public Dictionary<int, Feedback> Reviews { get; set; } = new();
        public Dictionary<int, string> ItemsByVisit { get; set; } = new();

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User)!;

            // Only completed visits can be reviewed
            Visits = (await _reservations.GetForCustomerAsync(userId))
                .Where(r => r.Status == ReservationStatus.Completed)
                .ToList();

            Reviews = (await _feedback.GetForCustomerAsync(userId))
                .Where(f => f.ReservationId != null)
                .GroupBy(f => f.ReservationId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            // What they ate on each visit (from orders linked to the booking)
            ItemsByVisit = (await _orders.GetForCustomerAsync(userId))
                .Where(o => o.ReservationId != null && o.Status != OrderStatus.Cancelled)
                .GroupBy(o => o.ReservationId!.Value)
                .ToDictionary(g => g.Key,
                              g => string.Join(", ", g.SelectMany(o => o.OrderItems)
                                                      .Select(i => $"{i.Quantity}x {i.MenuItem?.Name}")));

            // Not yet reviewed first
            Visits = Visits.OrderBy(v => Reviews.ContainsKey(v.Id)).ThenByDescending(v => v.StartTime).ToList();
        }

        public async Task<IActionResult> OnPostSubmitAsync(int reservationId, int rating, string? comment)
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _feedback.SubmitAsync(userId, reservationId, rating,
                                                     string.IsNullOrWhiteSpace(comment) ? null : comment.Trim());

            if (result.Success) Message = "Thank you! Your review has been submitted.";
            else Error = result.Error;

            return RedirectToPage();
        }
    }
}
