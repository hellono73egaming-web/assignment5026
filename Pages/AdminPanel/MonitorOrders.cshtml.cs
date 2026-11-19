using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // Admin e: monitor customer orders and payment transactions (read-only).
    public class MonitorOrdersModel : PageModel
    {
        private readonly IOrderService _orders;
        private readonly IPaymentService _payments;

        public MonitorOrdersModel(IOrderService orders, IPaymentService payments)
        {
            _orders = orders;
            _payments = payments;
        }

        public List<Order> Orders { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
        public int ActiveCount { get; set; }
        public decimal PaidToday { get; set; }

        public async Task OnGetAsync()
        {
            Orders = await _orders.GetRecentAsync(100);
            Payments = (await _payments.GetAllAsync()).Take(100).ToList();

            ActiveCount = Orders.Count(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
            PaidToday = Payments.Where(p => p.Status == PaymentStatus.Paid && p.PaidAt?.Date == DateTime.Today).Sum(p => p.Amount);
        }

        // ---- Helpers used by the page ----
        public static string StatusCss(OrderStatus s) => s switch
        {
            OrderStatus.Pending => "badge-pending",
            OrderStatus.Preparing => "badge-preparing",
            OrderStatus.ReadyToServe => "badge-ready",
            OrderStatus.Served => "badge-served",
            OrderStatus.Completed => "badge-completed",
            _ => "badge-cancelled"
        };

        public static string PaymentCss(PaymentStatus s) => s == PaymentStatus.Paid ? "badge-paid" : "badge-unpaid";

        public static string PaymentLabel(PaymentStatus s) => s == PaymentStatus.Paid ? "Paid" : s == PaymentStatus.Pending ? "Unpaid" : s.ToString();

        public static string Method(PaymentMethod m) => m switch
        {
            PaymentMethod.Card => "Card",
            PaymentMethod.EWallet => "E-Wallet",
            PaymentMethod.OnlineBanking => "Online Banking",
            _ => "Cash"
        };

        public static string MethodIcon(PaymentMethod m) => m switch
        {
            PaymentMethod.Card => "fa-solid fa-credit-card",
            PaymentMethod.EWallet => "fa-solid fa-wallet",
            PaymentMethod.OnlineBanking => "fa-solid fa-building-columns",
            _ => "fa-solid fa-money-bill-wave"
        };

        public static string Ago(DateTime t)
        {
            var span = DateTime.Now - t;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (t.Date == DateTime.Today) return t.ToString("h:mm tt");
            return t.ToString("dd MMM, h:mm tt");
        }
    }
}
