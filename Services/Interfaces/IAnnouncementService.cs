using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // AD15: system announcements (e.g. holiday closures, new menu launches).
    public interface IAnnouncementService
    {
        Task<List<Announcement>> GetAllAsync();      // admin list
        Task<List<Announcement>> GetCurrentAsync();  // shown on customer/staff dashboards today
        Task<ServiceResult<Announcement>> SaveAsync(Announcement announcement, bool notifyCustomers);
        Task<ServiceResult> SetActiveAsync(int id, bool active);
        Task<ServiceResult> DeleteAsync(int id);
        Task<ServiceResult<int>> PublishAsync(int id);   // send as a notification to every customer
    }
}
