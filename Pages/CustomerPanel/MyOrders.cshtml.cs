using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // CS12 (order + payment history), CS13 (live order tracking), receipts.
    // Customers only ever see THEIR OWN orders.
    public class MyOrdersModel : PageModel
    {
        private readonly IOrderService _orders;
        private readonly IFeedbackService _feedback;
        private readonly IReservationService _reservations;
        private readonly UserManager<ApplicationUser> _userManager;

        public MyOrdersModel(IOrderService orders, IFeedbackService feedback,
                             IReservationService reservations, UserManager<ApplicationUser> userManager)
        {
            _orders = orders;
            _feedback = feedback;
            _reservations = reservations;
            _userManager = userManager;
        }

        public List<Order> ActiveOrders { get; set; } = new();
        public List<Order> PastOrders { get; set; } = new();
        public HashSet<int> RatedReservationIds { get; set; } = new();
        public RestaurantSetting Settings { get; set; } = new();

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            var all = await _orders.GetForCustomerAsync(userId);

            ActiveOrders = all.Where(IsActive).ToList();
            PastOrders = all.Where(o => !IsActive(o)).ToList();

            RatedReservationIds = (await _feedback.GetForCustomerAsync(userId))
                .Where(f => f.ReservationId != null)
                .Select(f => f.ReservationId!.Value)
                .ToHashSet();

            Settings = await _reservations.GetSettingsAsync();
        }

        public async Task<IActionResult> OnPostCancelAsync(int id, string orderNumber)
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _orders.CancelByCustomerAsync(id, userId);

            if (result.Success) Message = $"Order #{orderNumber} has been cancelled.";
            else Error = result.Error;

            return RedirectToPage();
        }

        // ---- Helpers used by the page ----
        public static bool IsActive(Order o) =>
            o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled;

        public static bool IsPaid(Order o) => o.Payment?.Status == PaymentStatus.Paid;

        // A visit can be reviewed once its booking is completed and not rated yet
        public bool CanReview(Order o) =>
            o.ReservationId != null
            && o.Reservation?.Status == ReservationStatus.Completed
            && !RatedReservationIds.Contains(o.ReservationId.Value);

        public bool IsReviewed(Order o) =>
            o.ReservationId != null && RatedReservationIds.Contains(o.ReservationId.Value);

        // Tracker step for the progress bar: 0 Received, 1 Preparing, 2 Ready, 3 Served
        public static int Step(OrderStatus s) => s switch
        {
            OrderStatus.Pending => 0,
            OrderStatus.Preparing => 1,
            OrderStatus.ReadyToServe => 2,
            _ => 3
        };

        public static string BadgeCss(OrderStatus s) => s switch
        {
            OrderStatus.Completed => "badge-completed",
            OrderStatus.Cancelled => "badge-cancelled",
            OrderStatus.ReadyToServe or OrderStatus.Served => "badge-ready",
            _ => "badge-preparing"
        };

        public static string TableLabel(Order o) =>
            o.Reservation?.RestaurantTable?.TableNumber
            ?? (o.OrderType == OrderType.Takeaway ? "Takeaway" : "Dine-in");

        public static string MethodLabel(Order o) =>
            o.Payment == null ? "Pay at Counter" : o.Payment.Method switch
            {
                PaymentMethod.Card => "Card",
                PaymentMethod.EWallet => "E-Wallet",
                PaymentMethod.OnlineBanking => "Online Banking",
                _ => IsPaid(o) ? "Cash" : "Pay at Counter"
            };
    }
}
