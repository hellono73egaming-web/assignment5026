using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // Customer dashboard: next booking, live order tracker and notifications (Customer g, i).
    // Presentation tier only: asks the service layer for data, never queries the database.
    public class IndexModel : PageModel
    {
        private readonly IDashboardService _dashboard;
        private readonly IReservationService _reservations;
        private readonly INotificationService _notifications;
        private readonly IPromotionService _promotions;
        private readonly IRecommendationService _recommendations;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(IDashboardService dashboard, IReservationService reservations,
                          INotificationService notifications, IPromotionService promotions,
                          IRecommendationService recommendations, UserManager<ApplicationUser> userManager)
        {
            _dashboard = dashboard;
            _reservations = reservations;
            _notifications = notifications;
            _promotions = promotions;
            _recommendations = recommendations;
            _userManager = userManager;
        }

        public Customer? CurrentCustomer { get; set; }
        public Reservation? NextReservation { get; set; }
        public int MoreReservations { get; set; }
        public Order? ActiveOrder { get; set; }
        public List<Notification> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
        public List<PromoCard> ActivePromotions { get; set; } = new();      // CS21
        public List<RecCard> Recommendations { get; set; } = new();          // Advanced 7

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return;

            var data = await _dashboard.GetCustomerDashboardAsync(userId);
            CurrentCustomer = data.Customer;
            NextReservation = data.UpcomingReservations.FirstOrDefault();
            MoreReservations = Math.Max(0, data.UpcomingReservations.Count - 1);
            ActiveOrder = data.ActiveOrder;
            Notifications = data.Notifications;
            UnreadCount = data.UnreadCount;

            ActivePromotions = (await _promotions.GetActiveAsync()).Select(PromoCard.From).ToList();
            Recommendations = (await _recommendations.GetForCustomerAsync(userId, 4)).Select(RecCard.From).ToList();
        }

        // Cancel the upcoming booking straight from the dashboard (same rules as My Reservations)
        public async Task<IActionResult> OnPostCancelAsync(int id)
        {
            var result = await _reservations.CancelAsync(id, _userManager.GetUserId(User));
            if (result.Success) Message = "Your reservation has been cancelled.";
            else Error = result.Error;
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMarkReadAsync()
        {
            await _notifications.MarkAllReadAsync(_userManager.GetUserId(User)!);
            return RedirectToPage();
        }

        // ---- Cards shown on the page ----
        public class PromoCard
        {
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string? ImageUrl { get; set; }
            public string DiscountType { get; set; } = "Percent";   // "Percent" or "Fixed"
            public decimal DiscountValue { get; set; }
            public DateTime EndDate { get; set; }

            public static PromoCard From(Promotion p)
            {
                var scope = p.MenuItem != null ? $"On {p.MenuItem.Name}." : p.MenuCategory != null ? $"On all {p.MenuCategory.Name}." : "On your whole order.";
                var desc = string.Join(" ", new[] { p.Description, scope, p.PromoCode != null ? $"Use code {p.PromoCode} at checkout." : null }
                                             .Where(x => !string.IsNullOrWhiteSpace(x)));
                return new PromoCard
                {
                    Title = p.Title,
                    Description = desc,
                    ImageUrl = p.MenuItem?.ImageUrl,       // item promos show the dish photo
                    DiscountType = p.DiscountType == PromotionDiscountType.Fixed ? "Fixed" : "Percent",
                    DiscountValue = p.DiscountType == PromotionDiscountType.Fixed ? p.DiscountAmount : p.DiscountPercent,
                    EndDate = p.EndDate
                };
            }
        }

        public class RecCard
        {
            public string Name { get; set; } = string.Empty;
            public string? ImageUrl { get; set; }
            public string Reason { get; set; } = string.Empty;
            public string Icon { get; set; } = "fa-star";
            public decimal Price { get; set; }

            public static RecCard From(Services.Recommendation r) => new()
            {
                Name = r.Item.Name,
                ImageUrl = r.Item.ImageUrl,
                Reason = r.Reason,
                Price = r.Item.Price,
                Icon = r.Reason.StartsWith("Order again") ? "fa-rotate-right"
                     : r.Reason.StartsWith("Because") ? "fa-heart"
                     : r.Reason.StartsWith("Popular") ? "fa-fire"
                     : "fa-star"
            };
        }

        // ---- Helpers used by the page ----
        public string FirstName =>
            (CurrentCustomer?.FullName ?? "Guest").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Guest";

        public static string ReservationBadge(ReservationStatus s) => s switch
        {
            ReservationStatus.Confirmed or ReservationStatus.Seated => "badge-confirmed",
            ReservationStatus.Pending => "badge-preparing",
            _ => "badge-muted"
        };

        // 0 Received, 1 Preparing, 2 Ready, 3 Served
        public static int Step(OrderStatus s) => s switch
        {
            OrderStatus.Pending => 0,
            OrderStatus.Preparing => 1,
            OrderStatus.ReadyToServe => 2,
            _ => 3
        };

        public static string NotificationIcon(NotificationType t) => t switch
        {
            NotificationType.Reservation => "fa-calendar-check",
            NotificationType.Order => "fa-utensils",
            NotificationType.Payment => "fa-receipt",
            NotificationType.Promotion => "fa-tag",
            _ => "fa-bell"
        };

        public static string Ago(DateTime t)
        {
            var span = DateTime.Now - t;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
            return t.ToString("d MMM, h:mm tt");
        }
    }
}
