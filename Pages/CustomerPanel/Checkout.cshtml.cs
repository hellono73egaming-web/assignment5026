using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // CS10/CS11 (place order), CS14 (payment record), Intermediate #3 (receipt).
    // Flow: Menu cart -> Review (server re-prices everything) -> Confirm -> Receipt.
    // Payment: "Counter" records a Pending cash payment that staff settle later;
    // "Online" (Intermediate 4) pays straight away through PaymentService.PayOnlineAsync.
    // The online payment is simulated: only the chosen METHOD is sent to the server,
    // card numbers are never posted or stored.
    public class CheckoutModel : PageModel
    {
        private readonly IOrderService _orders;
        private readonly IPaymentService _payments;
        private readonly IReservationService _reservations;
        private readonly UserManager<ApplicationUser> _userManager;

        public CheckoutModel(IOrderService orders, IPaymentService payments,
                             IReservationService reservations, UserManager<ApplicationUser> userManager)
        {
            _orders = orders;
            _payments = payments;
            _reservations = reservations;
            _userManager = userManager;
        }

        // Carried from the menu page through review to confirm
        [BindProperty] public string? CartJson { get; set; }
        [BindProperty] public OrderType OrderType { get; set; } = OrderType.DineIn;
        [BindProperty] public int? ReservationId { get; set; }
        [BindProperty] public string? TableNumber { get; set; }
        [BindProperty] public string? Notes { get; set; }
        [BindProperty] public string? PromoCode { get; set; }                           // optional promotion code
        [BindProperty] public string PaymentOption { get; set; } = "Counter";          // "Counter" or "Online"
        [BindProperty] public PaymentMethod OnlineMethod { get; set; } = PaymentMethod.Card;

        [TempData] public string? PaymentMessage { get; set; }
        [TempData] public string? PaymentError { get; set; }

        public OrderQuote Quote { get; set; } = new();
        public Reservation? LinkedReservation { get; set; }
        public RestaurantSetting Settings { get; set; } = new();
        public Order? PlacedOrder { get; set; }
        public string? Error { get; set; }

        // Nothing to check out when opened directly
        public IActionResult OnGet() => RedirectToPage("/CustomerPanel/Menu");

        // Step 1: show the order summary with server-side prices
        public async Task<IActionResult> OnPostReviewAsync()
        {
            await LoadCommonAsync();

            var lines = ParseCart(CartJson);
            if (lines.Count == 0) return RedirectToPage("/CustomerPanel/Menu");

            PromoCode = string.IsNullOrWhiteSpace(PromoCode) ? null : PromoCode.Trim().ToUpperInvariant();
            Quote = await _orders.CalculateAsync(lines, PromoCode);
            if (Quote.Problems.Count > 0) Error = string.Join(" ", Quote.Problems);
            if (Quote.PromoCodeError != null) PromoCode = null;   // don't carry a code that didn't work
            if (Quote.IsEmpty) Error ??= "Your cart is empty.";

            return Page();
        }

        // Step 2: place the order and record a Pending "pay at counter" payment
        public async Task<IActionResult> OnPostConfirmAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            var lines = ParseCart(CartJson);
            if (lines.Count == 0) return RedirectToPage("/CustomerPanel/Menu");

            if (OrderType == OrderType.Takeaway) ReservationId = null;

            // Walk-in table number (no booking) is kept with the kitchen notes
            var notes = Notes?.Trim();
            if (!string.IsNullOrWhiteSpace(TableNumber) && ReservationId == null)
                notes = $"Table {TableNumber.Trim().ToUpper()}" + (string.IsNullOrEmpty(notes) ? "" : $". {notes}");

            var result = await _orders.PlaceOrderAsync(userId, lines, OrderType, ReservationId, notes, PromoCode);
            if (!result.Success)
            {
                await LoadCommonAsync();
                Quote = await _orders.CalculateAsync(lines, PromoCode);
                Error = result.Error;
                return Page();
            }

            var order = result.Value!;

            if (PaymentOption == "Online")
            {
                var paid = await _payments.PayOnlineAsync(order.Id, userId, OnlineMethod);
                if (paid.Success)
                {
                    PaymentMessage = $"Payment successful. Reference {paid.Value!.TransactionReference}.";
                }
                else
                {
                    // Order is still placed; fall back to paying at the counter
                    await _payments.RecordPaymentAsync(order.Id, PaymentMethod.Cash, PaymentStatus.Pending, "Pay at counter");
                    PaymentError = $"Online payment failed ({paid.Error}). You can pay at the counter instead.";
                }
            }
            else
            {
                await _payments.RecordPaymentAsync(order.Id, PaymentMethod.Cash, PaymentStatus.Pending, "Pay at counter");
            }

            // Post/Redirect/Get: refreshing the receipt won't place the order twice
            return RedirectToPage(new { handler = "Receipt", orderId = order.Id });
        }

        // Step 3: receipt (only for the customer's own order)
        public async Task<IActionResult> OnGetReceiptAsync(int orderId)
        {
            var userId = _userManager.GetUserId(User)!;
            var order = await _orders.GetWithDetailsAsync(orderId);
            if (order == null || order.CustomerId != userId) return RedirectToPage("/CustomerPanel/Menu");

            Settings = await _reservations.GetSettingsAsync();
            PlacedOrder = order;
            LinkedReservation = order.Reservation;
            OrderType = order.OrderType;
            return Page();
        }

        // Pay an existing unpaid order online (from the receipt or My Orders)
        public async Task<IActionResult> OnPostPayOnlineAsync(int orderId, PaymentMethod method)
        {
            var userId = _userManager.GetUserId(User)!;
            var paid = await _payments.PayOnlineAsync(orderId, userId, method);

            if (paid.Success) PaymentMessage = $"Payment successful. Reference {paid.Value!.TransactionReference}.";
            else PaymentError = paid.Error;

            return RedirectToPage(new { handler = "Receipt", orderId });
        }

        // ---- helpers ----
        public static string MethodName(PaymentMethod m) => m switch
        {
            PaymentMethod.Card => "Card",
            PaymentMethod.EWallet => "E-Wallet",
            PaymentMethod.OnlineBanking => "Online Banking",
            _ => "Cash"
        };

        private async Task LoadCommonAsync()
        {
            Settings = await _reservations.GetSettingsAsync();
            if (ReservationId != null && OrderType == OrderType.DineIn)
            {
                var userId = _userManager.GetUserId(User)!;
                LinkedReservation = (await _reservations.GetForCustomerAsync(userId))
                    .FirstOrDefault(r => r.Id == ReservationId);
            }
        }

        private static List<CartLine> ParseCart(string? json)
        {
            try
            {
                var lines = JsonSerializer.Deserialize<List<CartLine>>(json ?? "[]",
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

                return lines
                    .Where(l => l.MenuItemId > 0 && l.Quantity > 0)
                    .Select(l => l with { Quantity = Math.Min(l.Quantity, CartService.MaxQuantityPerItem), Remarks = null })
                    .ToList();
            }
            catch (JsonException)
            {
                return new List<CartLine>();
            }
        }
    }
}
