using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // A priced line, used for the cart page and the order summary
    public class QuoteLine
    {
        public int MenuItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public MenuItemType ItemType { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }          // item-level promotion
        public string? PromotionTitle { get; set; }
        public string? Remarks { get; set; }
        public decimal LineTotal => UnitPrice * Quantity - Discount;
    }

    // Full price breakdown of a cart, BEFORE it becomes an order
    public class OrderQuote
    {
        public List<QuoteLine> Lines { get; set; } = new();
        public List<string> Problems { get; set; } = new();   // e.g. "Cendol is no longer available"
        public decimal Subtotal { get; set; }
        public decimal Discount { get; set; }
        public string? OrderPromotionTitle { get; set; }
        public string? AppliedPromoCode { get; set; }     // code the customer entered and that gave a discount
        public string? PromoCodeError { get; set; }       // e.g. "Code XYZ is invalid or has expired."
        public decimal TaxPercent { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public bool IsEmpty => Lines.Count == 0;
    }

    // CS10, CS11, CS13, CS16, RS9-RS12, AD9
    // Approach: domain service that turns a basket into an order and freezes item prices,
    // similar in structure to BasketService/OrderService in Microsoft's eShopOnWeb.
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public OrderService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        // RS12 - allowed order status changes (state machine)
        private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
        {
            [OrderStatus.Pending]      = new[] { OrderStatus.Preparing, OrderStatus.Cancelled },
            [OrderStatus.Preparing]    = new[] { OrderStatus.ReadyToServe, OrderStatus.Pending, OrderStatus.Cancelled },   // Pending = kitchen "undo"
            [OrderStatus.ReadyToServe] = new[] { OrderStatus.Served, OrderStatus.Preparing },                         // Preparing = kitchen "undo"
            [OrderStatus.Served]       = new[] { OrderStatus.Completed },
            [OrderStatus.Completed]    = Array.Empty<OrderStatus>(),
            [OrderStatus.Cancelled]    = Array.Empty<OrderStatus>()
        };

        public static IReadOnlyList<OrderStatus> GetNextStatuses(OrderStatus current) =>
            AllowedTransitions[current];

        // ------------------------------------------------------------
        // PRICING - prices ALWAYS come from the database, never from the browser
        // ------------------------------------------------------------
        // Promotions: per item, per category, or whole order; Percent or Fixed RM.
        // Promotions with a PromoCode only count when that code is entered at checkout.
        // Each line gets its single best promotion, then the best whole-order promotion is applied.
        public async Task<OrderQuote> CalculateAsync(IEnumerable<CartLine> cartLines, string? promoCode = null)
        {
            var quote = new OrderQuote();
            var lines = cartLines.Where(l => l.Quantity > 0).ToList();
            if (lines.Count == 0) return quote;

            var ids = lines.Select(l => l.MenuItemId).Distinct().ToList();
            var items = await _db.MenuItems.AsNoTracking()
                .Where(m => ids.Contains(m.Id))
                .ToDictionaryAsync(m => m.Id);

            // Load active promotions, then check dates and codes in memory
            var today = DateTime.Today;
            var code = string.IsNullOrWhiteSpace(promoCode) ? null : promoCode.Trim().ToUpperInvariant();
            var promotions = (await _db.Promotions.AsNoTracking()
                    .Where(p => p.IsActive && p.StartDate <= today && p.EndDate >= today)
                    .ToListAsync())
                .Where(p => p.IsCurrentlyValid)
                .Where(p => p.PromoCode == null || (code != null && p.PromoCode.ToUpperInvariant() == code))
                .ToList();

            if (code != null && !promotions.Any(p => p.PromoCode != null))
                quote.PromoCodeError = $"Code {code} is invalid or has expired.";

            foreach (var line in lines)
            {
                if (!items.TryGetValue(line.MenuItemId, out var item))
                {
                    quote.Problems.Add("An item in your cart no longer exists and was skipped.");
                    continue;
                }
                if (!item.IsAvailable)
                {
                    quote.Problems.Add($"{item.Name} is currently unavailable and was skipped.");
                    continue;
                }



                // Best promotion for this item: one for the item itself, or for its category
                var promo = promotions
                    .Where(p => p.MenuItemId == item.Id || (p.MenuItemId == null && p.MenuCategoryId == item.MenuCategoryId))
                    .OrderByDescending(p => LineDiscount(p, item.Price, line.Quantity))
                    .FirstOrDefault();

                quote.Lines.Add(new QuoteLine
                {
                    MenuItemId = item.Id,
                    Name = item.Name,
                    ImageUrl = item.ImageUrl,
                    ItemType = item.ItemType,
                    Quantity = line.Quantity,
                    UnitPrice = item.Price,
                    Discount = promo == null ? 0 : LineDiscount(promo, item.Price, line.Quantity),
                    PromotionTitle = promo?.Title,
                    Remarks = line.Remarks
                });
            }

            quote.Subtotal = quote.Lines.Sum(l => l.UnitPrice * l.Quantity);
            var itemDiscounts = quote.Lines.Sum(l => l.Discount);

            // Best whole-order promotion (no item, no category) on what is left
            var remaining = quote.Subtotal - itemDiscounts;
            var orderPromo = promotions
                .Where(p => p.MenuItemId == null && p.MenuCategoryId == null)
                .OrderByDescending(p => OrderDiscount(p, remaining))
                .FirstOrDefault();

            var orderDiscount = orderPromo == null ? 0 : OrderDiscount(orderPromo, remaining);

            quote.OrderPromotionTitle = orderPromo?.Title;
            quote.Discount = itemDiscounts + orderDiscount;

            // Did the entered code actually save anything?
            if (code != null && quote.PromoCodeError == null)
            {
                var codeUsed = quote.Lines.Any(l => l.Discount > 0 && promotions.Any(p => p.PromoCode != null && p.Title == l.PromotionTitle))
                            || (orderDiscount > 0 && orderPromo?.PromoCode != null);
                if (codeUsed) quote.AppliedPromoCode = code;
                else quote.PromoCodeError = $"Code {code} doesn't apply to the items in your cart.";
            }

            var settings = await _db.RestaurantSettings.AsNoTracking().FirstOrDefaultAsync();
            quote.TaxPercent = settings?.ServiceTaxPercent ?? 0;
            quote.Tax = Round((quote.Subtotal - quote.Discount) * quote.TaxPercent / 100);
            quote.Total = quote.Subtotal - quote.Discount + quote.Tax;

            return quote;
        }

        // ------------------------------------------------------------
        // CUSTOMER ACTIONS
        // ------------------------------------------------------------

        // CS10 / CS11 - turn the cart into an order
        public async Task<ServiceResult<Order>> PlaceOrderAsync(string customerId, IEnumerable<CartLine> cartLines,
                                                                OrderType orderType, int? reservationId = null,
                                                                string? notes = null, string? promoCode = null)
        {
            var quote = await CalculateAsync(cartLines, promoCode);

            if (quote.Problems.Count > 0)
                return ServiceResult<Order>.Fail(string.Join(" ", quote.Problems) + " Please review your cart.");
            if (quote.IsEmpty)
                return ServiceResult<Order>.Fail("Your cart is empty.");

            if (reservationId != null)
            {
                var reservation = await _db.Reservations.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == reservationId && r.CustomerId == customerId);

                if (reservation == null)
                    return ServiceResult<Order>.Fail("Reservation not found.");
                if (reservation.Status is ReservationStatus.Cancelled or ReservationStatus.NoShow or ReservationStatus.Completed)
                    return ServiceResult<Order>.Fail("You cannot order for a cancelled or finished reservation.");
            }

            var order = new Order
            {
                OrderNumber = ReservationService.GenerateCode("ORD"),
                CustomerId = customerId,
                ReservationId = reservationId,
                OrderType = orderType,
                Status = OrderStatus.Pending,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                Subtotal = quote.Subtotal,
                Discount = quote.Discount,
                Tax = quote.Tax,
                Total = quote.Total
            };

            foreach (var line in quote.Lines)
            {
                order.OrderItems.Add(new OrderItem
                {
                    MenuItemId = line.MenuItemId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,   // price frozen at time of order
                    Remarks = line.Remarks
                });
            }

            _db.Orders.Add(order);
            _notifications.Queue(customerId, "Order placed",
                $"Order {order.OrderNumber} (RM {order.Total:N2}) has been sent to the kitchen.",
                NotificationType.Order);

            await _db.SaveChangesAsync();
            return ServiceResult<Order>.Ok(order);
        }

        // Customer may cancel only before the kitchen starts
        public async Task<ServiceResult> CancelByCustomerAsync(int orderId, string customerId)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);
            if (order == null) return ServiceResult.Fail("Order not found.");
            if (order.Status != OrderStatus.Pending)
                return ServiceResult.Fail("This order is already being prepared and can no longer be cancelled. Please ask our staff.");

            return await UpdateStatusAsync(orderId, OrderStatus.Cancelled);
        }

        // ------------------------------------------------------------
        // STAFF ACTIONS
        // ------------------------------------------------------------

        // RS10-RS12 - move an order through its statuses
        public async Task<ServiceResult> UpdateStatusAsync(int orderId, OrderStatus newStatus)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return ServiceResult.Fail("Order not found.");

            if (!AllowedTransitions[order.Status].Contains(newStatus))
                return ServiceResult.Fail($"Cannot change an order from {order.Status} to {newStatus}.");

            order.Status = newStatus;
            order.UpdatedAt = DateTime.Now;

            var message = newStatus switch
            {
                OrderStatus.Preparing    => $"The kitchen is now preparing order {order.OrderNumber}.",
                OrderStatus.ReadyToServe => $"Order {order.OrderNumber} is ready!",
                OrderStatus.Served       => $"Order {order.OrderNumber} has been served. Enjoy your meal!",
                OrderStatus.Completed    => $"Order {order.OrderNumber} is complete. Thank you!",
                OrderStatus.Cancelled    => $"Order {order.OrderNumber} has been cancelled.",
                _ => null
            };

            if (message != null)
                _notifications.Queue(order.CustomerId, $"Order {FriendlyStatus(newStatus).ToLower()}",
                    message, NotificationType.Order);

            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // ------------------------------------------------------------
        // QUERIES
        // ------------------------------------------------------------

        public Task<Order?> GetWithDetailsAsync(int orderId) =>
            _db.Orders
               .Include(o => o.Customer)
               .Include(o => o.Reservation).ThenInclude(r => r!.RestaurantTable)
               .Include(o => o.OrderItems).ThenInclude(i => i.MenuItem)
               .Include(o => o.Payment)
               .FirstOrDefaultAsync(o => o.Id == orderId);

        // CS13 - customer's order history
        public Task<List<Order>> GetForCustomerAsync(string customerId) =>
            _db.Orders
               .Include(o => o.OrderItems).ThenInclude(i => i.MenuItem)
               .Include(o => o.Payment)
               .Include(o => o.Reservation).ThenInclude(r => r!.RestaurantTable)
               .Where(o => o.CustomerId == customerId)
               .OrderByDescending(o => o.CreatedAt)
               .ToListAsync();

        // AD5 - admin live monitor: latest orders of any status
        public Task<List<Order>> GetRecentAsync(int take = 100) =>
            _db.Orders
               .Include(o => o.Customer)
               .Include(o => o.OrderItems).ThenInclude(i => i.MenuItem)
               .Include(o => o.Reservation).ThenInclude(r => r!.RestaurantTable)
               .OrderByDescending(o => o.CreatedAt)
               .Take(take)
               .ToListAsync();

        // RS9 - kitchen / staff board: everything not finished yet, oldest first
        public Task<List<Order>> GetActiveOrdersAsync() =>
            _db.Orders
               .Include(o => o.Customer)
               .Include(o => o.Reservation).ThenInclude(r => r!.RestaurantTable)
               .Include(o => o.OrderItems).ThenInclude(i => i.MenuItem)
               .Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
               .OrderBy(o => o.CreatedAt)
               .ToListAsync();

        // RS13/RS14 - billing screen: every unpaid (non-cancelled) order, plus orders paid today
        public Task<List<Order>> GetBillingListAsync()
        {
            var today = DateTime.Today;
            return _db.Orders
               .Include(o => o.Customer)
               .Include(o => o.Reservation).ThenInclude(r => r!.RestaurantTable)
               .Include(o => o.OrderItems).ThenInclude(i => i.MenuItem)
               .Include(o => o.Payment)
               .Where(o => o.Status != OrderStatus.Cancelled
                           && (o.Payment == null || o.Payment.Status != PaymentStatus.Paid
                               || o.Payment.PaidAt >= today))
               .OrderBy(o => o.Payment != null && o.Payment.Status == PaymentStatus.Paid)   // unpaid first
               .ThenBy(o => o.CreatedAt)
               .ToListAsync();
        }

        public static string FriendlyStatus(OrderStatus status) => status switch
        {
            OrderStatus.ReadyToServe => "Ready to Serve",
            _ => status.ToString()
        };

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        // Discount a promotion gives on one cart line (Fixed = RM off each unit, never below RM 0)
        private static decimal LineDiscount(Promotion p, decimal unitPrice, int qty) =>
            p.DiscountType == PromotionDiscountType.Fixed
                ? Round(Math.Min(p.DiscountAmount, unitPrice) * qty)
                : Round(unitPrice * qty * p.DiscountPercent / 100);

        // Discount a whole-order promotion gives on the amount left after item discounts
        private static decimal OrderDiscount(Promotion p, decimal amount) =>
            p.DiscountType == PromotionDiscountType.Fixed
                ? Round(Math.Min(p.DiscountAmount, amount))
                : Round(amount * p.DiscountPercent / 100);
    }
}
