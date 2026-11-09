using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // Abstraction for payments and receipts (RS13, RS14, CS14, AD10).
    // Customers pay at the counter (Cash / E-Wallet) and staff record it. Because pages depend on
    // this interface, an online gateway could be added later without changing any page code.
    public interface IPaymentService
    {
        Task<ServiceResult<Payment>> RecordPaymentAsync(int orderId, PaymentMethod method,
                                                        PaymentStatus status, string? reference = null);
        Task<ServiceResult<Payment>> SettleBillAsync(int orderId, PaymentMethod method);      // RS13: staff takes payment
        Task<ServiceResult> AdjustBillAsync(int orderId, decimal newTotal, string reason);      // RS14: manager adjustment
        Task<Payment?> GetReceiptAsync(int orderId);
        Task<List<Payment>> GetForCustomerAsync(string customerId);
        Task<List<Payment>> GetAllAsync(DateTime? from = null, DateTime? to = null);
    }
}
