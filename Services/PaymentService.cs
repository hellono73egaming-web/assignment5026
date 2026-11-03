using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // RS13, RS14, CS14, CS22, AD10, Intermediate #3 (receipts) and #4 (online payment + tracking)
    // Approach: payment logic behind an interface so the simulated gateway can be swapped
    // for a real provider later (Dependency Inversion, as in eShopOnWeb).
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

        // CS - online payment by the customer.
        // SIMULATED gateway: no real card is charged. A real system would call
        // Stripe / iPay88 / Billplz here and use their transaction reference.
        public async Task<ServiceResult<Payment>> PayOnlineAsync(int orderId, string customerId, PaymentMethod method)
        {
            if (method == PaymentMethod.Cash)
                return ServiceResult<Payment>.Fail("Cash payments are made at the counter.");

            var order = await _db.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId);

            if (order == null) return ServiceResult<Payment>.Fail("Order not found.");
            if (order.Status == OrderStatus.Cancelled) return ServiceResult<Payment>.Fail("This order was cancelled.");
            if (order.Payment?.Status == PaymentStatus.Paid) return ServiceResult<Payment>.Fail("This order is already paid.");

            var transactionRef = $"TXN-{Guid.NewGuid().ToString("N")[..12].ToUpper()}";
            var payment = await MarkPaidAsync(order, method, transactionRef);
            return ServiceResult<Payment>.Ok(payment);
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
