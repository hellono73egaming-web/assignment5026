using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // Business tier for system announcements (AD15).
    // An announcement is "current" when it is switched on and today is between its start and (optional) end date.
    public class AnnouncementService : IAnnouncementService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public AnnouncementService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public Task<List<Announcement>> GetAllAsync() =>
            _db.Announcements
               .OrderByDescending(a => a.IsActive)
               .ThenByDescending(a => a.StartDate)
               .AsNoTracking()
               .ToListAsync();

        public Task<List<Announcement>> GetCurrentAsync()
        {
            var today = DateTime.Today;
            return _db.Announcements
               .Where(a => a.IsActive && a.StartDate.Date <= today && (a.EndDate == null || a.EndDate.Value.Date >= today))
               .OrderByDescending(a => a.StartDate)
               .AsNoTracking()
               .ToListAsync();
        }

        public async Task<ServiceResult<Announcement>> SaveAsync(Announcement input, bool notifyCustomers)
        {
            var title = input.Title?.Trim() ?? string.Empty;
            var content = input.Content?.Trim() ?? string.Empty;

            if (title.Length < 3 || title.Length > 150)
                return ServiceResult<Announcement>.Fail("Title must be between 3 and 150 characters.");
            if (content.Length < 5 || content.Length > 2000)
                return ServiceResult<Announcement>.Fail("Message must be between 5 and 2000 characters.");
            if (input.EndDate != null && input.EndDate.Value.Date < input.StartDate.Date)
                return ServiceResult<Announcement>.Fail("End date cannot be before the start date.");

            Announcement a;
            if (input.Id == 0)
            {
                a = new Announcement { CreatedAt = DateTime.Now };
                _db.Announcements.Add(a);
            }
            else
            {
                a = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == input.Id)
                    ?? throw new InvalidOperationException();
            }

            a.Title = title;
            a.Content = content;
            a.StartDate = input.StartDate.Date;
            a.EndDate = input.EndDate?.Date;
            a.IsActive = input.IsActive;
            a.Audience = input.Audience;

            await _db.SaveChangesAsync();

            if (notifyCustomers && a.IsActive) await PublishAsync(a.Id);
            return ServiceResult<Announcement>.Ok(a);
        }

        public async Task<ServiceResult> SetActiveAsync(int id, bool active)
        {
            var a = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return ServiceResult.Fail("Announcement not found.");
            a.IsActive = active;
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            var a = await _db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return ServiceResult.Fail("Announcement not found.");
            _db.Announcements.Remove(a);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // Send to every active customer's notification list
        public async Task<ServiceResult<int>> PublishAsync(int id)
        {
            var a = await _db.Announcements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return ServiceResult<int>.Fail("Announcement not found.");
            if (!a.IsActive) return ServiceResult<int>.Fail("Switch the announcement on before publishing it.");

            var message = a.Content.Length > 300 ? a.Content[..300] + "..." : a.Content;
            var sent = 0;
            if (a.Audience != AnnouncementAudience.Staff)
                sent += await _notifications.SendToAllCustomersAsync(a.Title, message, "/CustomerPanel/Index", NotificationType.General);
            if (a.Audience != AnnouncementAudience.Customers)
                sent += await _notifications.SendToAllStaffAsync(a.Title, message, "/StaffPanel/Index");
            return ServiceResult<int>.Ok(sent);
        }
    }
}
