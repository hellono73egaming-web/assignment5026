using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // Business tier for promotions and promotional campaigns.
    // A promotion is either for ONE menu item (MenuItemId set) or for the WHOLE order (MenuItemId null).
    // OrderService.CalculateAsync already picks the best valid promotion when pricing a cart,
    // so this service only has to manage them and announce them.
    public class PromotionService : IPromotionService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public PromotionService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public Task<List<Promotion>> GetAllAsync() =>
            _db.Promotions
               .Include(p => p.MenuItem)
               .Include(p => p.MenuCategory)
               .OrderByDescending(p => p.IsActive)
               .ThenByDescending(p => p.StartDate)
               .AsNoTracking()
               .ToListAsync();

        // Active today: switched on and today is inside the start/end dates
        public Task<List<Promotion>> GetActiveAsync()
        {
            var today = DateTime.Today;
            return _db.Promotions
               .Include(p => p.MenuItem)
               .Include(p => p.MenuCategory)
               .Where(p => p.IsActive && p.StartDate.Date <= today && p.EndDate.Date >= today)
               .OrderByDescending(p => p.DiscountPercent)
               .AsNoTracking()
               .ToListAsync();
        }

        public Task<Promotion?> GetAsync(int id) =>
            _db.Promotions.Include(p => p.MenuItem).Include(p => p.MenuCategory).FirstOrDefaultAsync(p => p.Id == id);

        public async Task<ServiceResult<Promotion>> SaveAsync(Promotion input, bool notifyCustomers)
        {
            // ---- validation ----
            var title = input.Title?.Trim() ?? string.Empty;
            if (title.Length < 3 || title.Length > 100)
                return ServiceResult<Promotion>.Fail("Title must be between 3 and 100 characters.");
            if (input.Description?.Length > 500)
                return ServiceResult<Promotion>.Fail("Description cannot be longer than 500 characters.");
            if (input.DiscountType == PromotionDiscountType.Percent && (input.DiscountPercent <= 0 || input.DiscountPercent > 100))
                return ServiceResult<Promotion>.Fail("Percentage discount must be more than 0% and at most 100%.");
            if (input.DiscountType == PromotionDiscountType.Fixed && (input.DiscountAmount <= 0 || input.DiscountAmount > 10000))
                return ServiceResult<Promotion>.Fail("Fixed discount must be more than RM 0.");

            // Promo code: optional, letters/numbers only, unique
            var code = string.IsNullOrWhiteSpace(input.PromoCode) ? null : input.PromoCode.Trim().ToUpperInvariant();
            if (code != null && !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9]{3,30}$"))
                return ServiceResult<Promotion>.Fail("Promo code must be 3-30 letters or numbers (no spaces).");
            if (code != null && await _db.Promotions.AnyAsync(p => p.Id != input.Id && p.PromoCode == code))
                return ServiceResult<Promotion>.Fail($"Promo code {code} is already used by another promotion.");
            if (input.MenuCategoryId != null && !await _db.MenuCategories.AnyAsync(c => c.Id == input.MenuCategoryId))
                return ServiceResult<Promotion>.Fail("The selected category no longer exists.");
            if (input.EndDate.Date < input.StartDate.Date)
                return ServiceResult<Promotion>.Fail("End date cannot be before the start date.");
            if (input.MenuItemId != null && !await _db.MenuItems.AnyAsync(m => m.Id == input.MenuItemId))
                return ServiceResult<Promotion>.Fail("The selected menu item no longer exists.");

            Promotion promo;
            if (input.Id == 0)
            {
                promo = new Promotion();
                _db.Promotions.Add(promo);
            }
            else
            {
                promo = await _db.Promotions.FirstOrDefaultAsync(p => p.Id == input.Id)
                        ?? throw new InvalidOperationException();
            }

            promo.Title = title;
            promo.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            promo.DiscountType = input.DiscountType;
            promo.DiscountPercent = input.DiscountType == PromotionDiscountType.Percent ? Math.Round(input.DiscountPercent, 2) : 0;
            promo.DiscountAmount = input.DiscountType == PromotionDiscountType.Fixed ? Math.Round(input.DiscountAmount, 2) : 0;
            promo.PromoCode = code;
            promo.MenuCategoryId = input.MenuItemId == null ? input.MenuCategoryId : null;   // one scope only
            promo.StartDate = input.StartDate.Date;
            promo.EndDate = input.EndDate.Date;
            promo.MenuItemId = input.MenuItemId;
            promo.IsActive = input.IsActive;

            await _db.SaveChangesAsync();

            // Campaign: tell every active customer (only if the promotion is live or upcoming)
            if (notifyCustomers && promo.IsActive && promo.EndDate.Date >= DateTime.Today)
                await SendCampaignAsync(promo.Id);

            return ServiceResult<Promotion>.Ok(promo);
        }

        public async Task<ServiceResult> SetActiveAsync(int id, bool active)
        {
            var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.Id == id);
            if (promo == null) return ServiceResult.Fail("Promotion not found.");

            promo.IsActive = active;
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            var promo = await _db.Promotions.FirstOrDefaultAsync(p => p.Id == id);
            if (promo == null) return ServiceResult.Fail("Promotion not found.");

            // Orders keep their own Discount amount, so removing a promotion never changes past bills
            _db.Promotions.Remove(promo);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // AD16 / CS21: broadcast the promotion as a notification to every active customer
        public async Task<ServiceResult<int>> SendCampaignAsync(int id)
        {
            var promo = await GetAsync(id);
            if (promo == null) return ServiceResult<int>.Fail("Promotion not found.");
            if (!promo.IsActive || promo.EndDate.Date < DateTime.Today)
                return ServiceResult<int>.Fail("Only active, unexpired promotions can be sent.");

            var target = promo.MenuItem != null ? $"on {promo.MenuItem.Name}"
                       : promo.MenuCategory != null ? $"on all {promo.MenuCategory.Name}"
                       : "on your whole order";
            var message = $"{promo.DiscountLabel} {target}, {promo.StartDate:d MMM} - {promo.EndDate:d MMM}."
                          + (promo.PromoCode != null ? $" Use code {promo.PromoCode} at checkout." : "")
                          + (string.IsNullOrEmpty(promo.Description) ? "" : " " + promo.Description);

            var sent = await _notifications.SendToAllCustomersAsync(promo.Title, message, "/CustomerPanel/Menu");
            return ServiceResult<int>.Ok(sent);
        }
    }
}
