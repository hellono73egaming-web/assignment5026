using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // Abstraction for table reservations and availability (CS7-CS9, RS4-RS8).
    public interface IReservationService
    {
        Task<RestaurantSetting> GetSettingsAsync();
        Task<string?> ValidateTimeSlotAsync(DateTime start, DateTime end);
        Task<bool> IsTableFreeAsync(int tableId, DateTime start, DateTime end, int? ignoreReservationId = null);
        Task<List<RestaurantTable>> GetAvailableTablesAsync(DateTime start, int partySize, int? ignoreReservationId = null);

        Task<ServiceResult<Reservation>> CreateAsync(string customerId, DateTime start, int partySize,
                                                     string? specialRequests, int? preferredTableId = null);
        Task<ServiceResult<Reservation>> ModifyAsync(int reservationId, DateTime start, int partySize,
                                                     string? specialRequests, int? tableId = null, string? customerId = null);
        Task<ServiceResult> CancelAsync(int reservationId, string? customerId = null);
        Task<ServiceResult> ChangeStatusAsync(int reservationId, ReservationStatus newStatus);

        Task<List<Reservation>> GetForDateAsync(DateTime date);
        Task<List<Reservation>> GetForCustomerAsync(string customerId);
    }
}
