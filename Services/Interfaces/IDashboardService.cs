using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // Abstraction for the three role dashboards. Keeps database queries out of the
    // Razor Pages so the presentation tier never talks to the data tier directly.
    public interface IDashboardService
    {
        Task<AdminDashboardStats> GetAdminStatsAsync();
        Task<StaffDashboardStats> GetStaffStatsAsync();
        Task<StaffDashboardData> GetStaffDashboardAsync();
        Task<CustomerDashboardData> GetCustomerDashboardAsync(string customerId);
    }
}
