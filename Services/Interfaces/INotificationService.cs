using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // Abstraction for in-system notifications (CS19-CS22).
    // Pages and other services depend on this interface, not on the concrete class
    // (Dependency Inversion Principle, as used in Microsoft's eShopOnWeb reference app).
    public interface INotificationService
    {
        void Queue(string userId, string title, string message, NotificationType type, string? link = null);
        Task SendAsync(string userId, string title, string message, NotificationType type, string? link = null);
        Task<int> SendToAllCustomersAsync(string title, string message, string? link = null,
                                          NotificationType type = NotificationType.Promotion);
        Task<int> GetUnreadCountAsync(string userId);
        Task<List<Notification>> GetLatestAsync(string userId, int take = 20);
        Task MarkAllReadAsync(string userId);
    }
}
