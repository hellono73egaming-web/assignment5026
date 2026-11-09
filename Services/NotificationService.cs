using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // CS19-CS22, Advanced #5 - in-system notifications.
    // Other services call this whenever something happens to a customer's
    // reservation, order or payment.
    // Approach: a dedicated service that other services call to record notifications,
    // registered through dependency injection (pattern from Microsoft eShopOnWeb).
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;

        public NotificationService(ApplicationDbContext db)
        {
            _db = db;
        }

        // Adds a notification to the context. It is saved together with the caller's
        // own SaveChangesAsync(), so the notification and the change succeed or fail together.
        public void Queue(string userId, string title, string message,
                          NotificationType type, string? link = null)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                Link = link
            });
        }

        // Adds and saves immediately.
        public async Task SendAsync(string userId, string title, string message,
                                    NotificationType type, string? link = null)
        {
            Queue(userId, title, message, type, link);
            await _db.SaveChangesAsync();
        }

        // CS21 - promotion broadcast to every active customer
        public async Task<int> SendToAllCustomersAsync(string title, string message, string? link = null,
                                                       NotificationType type = NotificationType.Promotion)
        {
            var customerIds = await _db.Customers
                .Where(c => c.IsActive)
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var id in customerIds)
                Queue(id, title, message, type, link);

            await _db.SaveChangesAsync();
            return customerIds.Count;
        }

        public Task<int> GetUnreadCountAsync(string userId) =>
            _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public Task<List<Notification>> GetLatestAsync(string userId, int take = 20) =>
            _db.Notifications
               .Where(n => n.UserId == userId)
               .OrderByDescending(n => n.CreatedAt)
               .Take(take)
               .ToListAsync();

        public async Task MarkAllReadAsync(string userId)
        {
            var unread = await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread) n.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }
}
