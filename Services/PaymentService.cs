using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // RS13, RS14, CS14, CS22, AD10, Intermediate #3 (receipts) and payment status tracking.
    // Payment flow: customer pays at the counter (Cash or E-Wallet), staff confirm it in Billing.
    // Payment logic sits behind an interface so an online gateway could be added later
    // without changing page code (Dependency Inversion, as in eShopOnWeb).
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        // 1 loyalty point per RM 1 spent
        public const decimal PointsPerRinggit = 1m;

        public PaymentService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        // RS13 - staff records a payment (e.g. cash at the counter) or changes its status
        public async Task<ServiceResult<Payment>> RecordPaymentAsync(int orderId, PaymentMethod method,
                                                                     PaymentStatus status, string? reference = null)
        {
            var order = await _db.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) return ServiceResult<Payment>.Fail("Order not found.");

            if (status == PaymentStatus.Paid)
            {
                if (order.Status == OrderStatus.Cancelled)
                    return ServiceResult<Payment>.Fail("Cannot take payment for a cancelled order.");
                if (order.Payment?.Status == PaymentStatus.Paid)
                    return ServiceResult<Payment>.Fail("This order is already paid.");

                var paid = await MarkPaidAsync(order, method, reference);
                return ServiceResult<Payment>.Ok(paid);
            }

            // Pending / Failed / Refunded
            if (status == PaymentStatus.Refunded && order.Payment?.Status != PaymentStatus.Paid)
                return ServiceResult<Payment>.Fail("Only a paid order can be refunded.");

            var payment = order.Payment ?? new Payment { OrderId = order.Id, Amount = order.Total };
            if (order.Payment == null) _db.Payments.Add(payment);

            payment.Method = method;
            payment.Status = status;
            payment.TransactionReference = reference ?? payment.TransactionReference;

            if (status == PaymentStatus.Refunded)
                _notifications.Queue(order.CustomerId, "Payment refunded",
                    $"RM {payment.Amount:N2} for order {order.OrderNumber} has been refunded.",
                    NotificationType.Payment);

            await _db.SaveChangesAsync();
            return ServiceResult<Payment>.Ok(payment);
        }

        private async Task<Payment> MarkPaidAsync(Order order, PaymentMethod method, string? reference)
        {
            var payment = order.Payment ?? new Payment { OrderId = order.Id };
            if (order.Payment == null) _db.Payments.Add(payment);

            payment.Amount = order.Total;
            payment.Method = method;
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTime.Now;
            payment.TransactionReference = reference;
            payment.ReceiptNumber ??= ReservationService.GenerateCode("RCP");

            // Loyalty points for the customer
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == order.CustomerId);
            var points = (int)Math.Floor(order.Total * PointsPerRinggit);
            if (customer != null) customer.LoyaltyPoints += points;

            _notifications.Queue(order.CustomerId, "Payment received",
                $"We received RM {payment.Amount:N2} for order {order.OrderNumber} by {method}. " +
                $"Receipt {payment.ReceiptNumber}. You earned {points} loyalty points.",
                NotificationType.Payment);

            await _db.SaveChangesAsync();
            return payment;
        }

        // RS13 - staff settles a bill at the counter.
        // A served order that is paid is finished, so it moves to Completed.
        public async Task<ServiceResult<Payment>> SettleBillAsync(int orderId, PaymentMethod method)
        {
            var result = await RecordPaymentAsync(orderId, method, PaymentStatus.Paid,
                                                  $"Counter-{DateTime.Now:yyyyMMddHHmmss}");
            if (!result.Success) return result;

            var order = await _db.Orders.FindAsync(orderId);
            if (order != null && order.Status == OrderStatus.Served)
            {
                order.Status = OrderStatus.Completed;
                order.UpdatedAt = DateTime.Now;
                await _db.SaveChangesAsync();
            }

            return result;
        }

        // RS14 - lower an unpaid bill (complaint, manager discount...). A reason is required
        // and recorded on the order, so every adjustment can be traced later.
        public async Task<ServiceResult> AdjustBillAsync(int orderId, decimal newTotal, string reason)
        {
            reason = reason?.Trim() ?? string.Empty;
            if (reason.Length < 3)
                return ServiceResult.Fail("Please give a reason for the adjustment.");
            if (reason.Length > 150)
                return ServiceResult.Fail("Reason can be at most 150 characters.");

            var order = await _db.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return ServiceResult.Fail("Order not found.");
            if (order.Status == OrderStatus.Cancelled) return ServiceResult.Fail("This order was cancelled.");
            if (order.Payment?.Status == PaymentStatus.Paid) return ServiceResult.Fail("This bill is already paid and can't be changed.");

            newTotal = Math.Round(newTotal, 2);
            if (newTotal <= 0) return ServiceResult.Fail("The new total must be more than RM 0.00.");
            if (newTotal >= order.Total) return ServiceResult.Fail("A bill can only be adjusted downwards.");

            var oldTotal = order.Total;
            order.Discount += oldTotal - newTotal;
            order.Total = newTotal;
            order.UpdatedAt = DateTime.Now;

            var note = $"[Bill adjusted RM {oldTotal:N2} -> RM {newTotal:N2}: {reason}]";
            var notes = string.IsNullOrWhiteSpace(order.Notes) ? note : $"{order.Notes} {note}";
            order.Notes = notes.Length > 500 ? notes[^500..] : notes;   // column limit

            if (order.Payment != null) order.Payment.Amount = newTotal;

            _notifications.Queue(order.CustomerId, "Bill updated",
                $"The bill for order {order.OrderNumber} was adjusted to RM {newTotal:N2} ({reason}).",
                NotificationType.Payment);

            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // Receipt page data (Intermediate #3)
        public Task<Payment?> GetReceiptAsync(int orderId) =>
            _db.Payments
               .Include(p => p.Order).ThenInclude(o => o!.Customer)
               .Include(p => p.Order).ThenInclude(o => o!.OrderItems).ThenInclude(i => i.MenuItem)
               .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == PaymentStatus.Paid);

        // CS14 - customer's payment records
        public Task<List<Payment>> GetForCustomerAsync(string customerId) =>
            _db.Payments
               .Include(p => p.Order)
               .Where(p => p.Order!.CustomerId == customerId)
               .OrderByDescending(p => p.CreatedAt)
               .ToListAsync();

        // AD10 - admin monitoring, optional date filter
        public Task<List<Payment>> GetAllAsync(DateTime? from = null, DateTime? to = null) =>
            _db.Payments
               .Include(p => p.Order).ThenInclude(o => o!.Customer)
               .Where(p => (from == null || p.CreatedAt >= from) && (to == null || p.CreatedAt < to))
               .OrderByDescending(p => p.CreatedAt)
               .ToListAsync();
    }
}
