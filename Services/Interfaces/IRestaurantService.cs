using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // AD2 / Admin b - manage dining areas, tables and opening hours.
    public interface IRestaurantService
    {
        // Dining areas
        Task<List<DiningArea>> GetAreasAsync();
        Task<ServiceResult> SaveAreaAsync(int id, string name, string? description, string? imageUrl);   // id 0 = add
        Task<ServiceResult> DeleteAreaAsync(int id);

        // Tables
        Task<List<RestaurantTable>> GetTablesAsync();
        Task<ServiceResult> SaveTableAsync(int id, string tableNumber, int capacity, int diningAreaId, bool isActive);   // id 0 = add
        Task<ServiceResult> SetTableActiveAsync(int id, bool isActive);

        // Opening hours (always 7 rows, Monday first)
        Task<List<OperatingHour>> GetOperatingHoursAsync();
        Task<ServiceResult> SaveOperatingHoursAsync(IEnumerable<OperatingHour> hours);
    }
}
