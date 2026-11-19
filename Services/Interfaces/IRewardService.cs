using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // Loyalty rewards: customers spend the points they earn on orders to buy vouchers,
    // then enter the voucher code at checkout.
    public interface IRewardService
    {
        Task<List<Voucher>> GetCatalogueAsync();
        Task<List<CustomerVoucher>> GetForCustomerAsync(string customerId);
        Task<int> GetPointsAsync(string customerId);

        // Spends the points and returns the new voucher with its code
        Task<ServiceResult<CustomerVoucher>> RedeemAsync(string customerId, int voucherId);

        // Admin: manage the rewards catalogue
        Task<List<Voucher>> GetAllAsync();
        Task<Voucher?> GetAsync(int id);
        Task<int> CountRedeemedAsync(int voucherId);
        Task<ServiceResult<Voucher>> SaveAsync(Voucher input);
        Task<ServiceResult> SetActiveAsync(int id, bool active);
        Task<ServiceResult> DeleteAsync(int id);
    }
}
