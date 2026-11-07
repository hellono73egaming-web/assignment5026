using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // Abstraction for ordering and order processing (CS10, CS11, CS13, CS16, RS9-RS12).
    public interface IOrderService
    {
        Task<OrderQuote> CalculateAsync(IEnumerable<CartLine> cartLines);
        Task<ServiceResult<Order>> PlaceOrderAsync(string customerId, IEnumerable<CartLine> cartLines,
                                                   OrderType orderType, int? reservationId = null, string? notes = null);
        Task<ServiceResult> CancelByCustomerAsync(int orderId, string customerId);
        Task<ServiceResult> UpdateStatusAsync(int orderId, OrderStatus newStatus);

        Task<Order?> GetWithDetailsAsync(int orderId);
        Task<List<Order>> GetForCustomerAsync(string customerId);
        Task<List<Order>> GetActiveOrdersAsync();
        Task<List<Order>> GetBillingListAsync();
        Task<List<Order>> GetRecentAsync(int take = 100);   // AD5 - admin monitor   // RS13/RS14: unpaid bills + bills paid today
    }
}
