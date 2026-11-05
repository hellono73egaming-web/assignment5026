using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // Abstraction for payments and receipts (RS13, RS14, CS14, AD10).
    // Because pages depend on this interface, the simulated gateway could later be
    // replaced by a real one (e.g. Stripe) without changing any page code.
    public interface IPaymentService
    {
        Task<ServiceResult<Payment>> PayOnlineAsync(int orderId, string customerId, PaymentMethod method);
        Task<ServiceResult<Payment>> RecordPaymentAsync(int orderId, PaymentMethod method,
                                                        PaymentStatus status, string? reference = null);
        Task<ServiceResult<Payment>> SettleBillAsync(int orderId, PaymentMethod method);      // RS13: staff takes payment
        Task<ServiceResult> AdjustBillAsync(int orderId, decimal newTotal, string reason);      // RS14: manager adjustment
        Task<Payment?> GetReceiptAsync(int orderId);
        Task<List<Payment>> GetForCustomerAsync(string customerId);
        Task<List<Payment>> GetAllAsync(DateTime? from = null, DateTime? to = null);
    }
}
