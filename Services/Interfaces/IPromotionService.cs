using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // AD6 (promotional offers), AD16 (promotional campaigns), CS21 (promotion notifications).
    // The discounts themselves are applied automatically at checkout by OrderService.CalculateAsync.
    public interface IPromotionService
    {
        Task<List<Promotion>> GetAllAsync();                 // admin list
        Task<List<Promotion>> GetActiveAsync();              // what customers see today
        Task<Promotion?> GetAsync(int id);

        // Create (Id = 0) or update. notifyCustomers = send it as a campaign to every customer.
        Task<ServiceResult<Promotion>> SaveAsync(Promotion promotion, bool notifyCustomers);
        Task<ServiceResult> SetActiveAsync(int id, bool active);
        Task<ServiceResult> DeleteAsync(int id);
        Task<ServiceResult<int>> SendCampaignAsync(int id);  // re-send an existing promotion to customers
    }
}
