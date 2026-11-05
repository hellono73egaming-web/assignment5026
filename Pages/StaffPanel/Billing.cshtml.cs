using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.StaffPanel
{
    // RS13 (record payment) and RS14 (manage billing).
    // Unpaid bills first, then bills paid today. Settling a served order completes it.
    public class BillingModel : PageModel
    {
        private readonly IOrderService _orders;
        private readonly IPaymentService _payments;
        private readonly IReservationService _reservations;

        public BillingModel(IOrderService orders, IPaymentService payments, IReservationService reservations)
        {
            _orders = orders;
            _payments = payments;
            _reservations = reservations;
        }

        public List<Order> Bills { get; set; } = new();
        public RestaurantSetting Settings { get; set; } = new();

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            Bills = await _orders.GetBillingListAsync();
            Settings = await _reservations.GetSettingsAsync();
        }

        public async Task<IActionResult> OnPostSettleAsync(int id, PaymentMethod method, string orderNumber)
        {
            var result = await _payments.SettleBillAsync(id, method);
            if (result.Success)
                Message = $"Payment of RM {result.Value!.Amount:N2} for #{orderNumber} received via {Friendly(method)}.";
            else
                Error = result.Error;
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAdjustAsync(int id, decimal newTotal, string? reason, string? customReason, string orderNumber)
        {
            var finalReason = reason == "Other" ? customReason : reason;
            var result = await _payments.AdjustBillAsync(id, newTotal, finalReason ?? string.Empty);
            if (result.Success)
                Message = $"Bill for #{orderNumber} updated to RM {newTotal:N2}.";
            else
                Error = result.Error;
            return RedirectToPage();
        }

        // ---- Helpers used by the page ----
        public static bool IsPaid(Order o) => o.Payment?.Status == PaymentStatus.Paid;

        public static string TableLabel(Order o) =>
            o.Reservation?.RestaurantTable?.TableNumber
            ?? (o.OrderType == OrderType.Takeaway ? "Takeaway" : "Dine-in");

        public static string Friendly(PaymentMethod m) => m switch
        {
            PaymentMethod.Card => "Card",
            PaymentMethod.EWallet => "E-Wallet",
            PaymentMethod.OnlineBanking => "Online Banking",
            _ => "Cash"
        };
    }
}
