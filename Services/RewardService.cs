using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // Rewards page logic. Points are earned in PaymentService (1 point per RM 1 paid)
    // and spent here. Using a voucher at checkout is handled by OrderService.
    public class RewardService : IRewardService
    {
        // No 0/O or 1/I so codes are easy to read out and type
        private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public RewardService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public Task<List<Voucher>> GetCatalogueAsync() =>
            _db.Vouchers.AsNoTracking().Include(v => v.MenuItem).Where(v => v.IsActive).OrderBy(v => v.PointsCost).ToListAsync();

        public Task<List<CustomerVoucher>> GetForCustomerAsync(string customerId) =>
            _db.CustomerVouchers.AsNoTracking()
               .Include(cv => cv.Voucher)
               .Include(cv => cv.Order)
               .Where(cv => cv.CustomerId == customerId)
               .OrderByDescending(cv => cv.RedeemedAt)
               .ToListAsync();

        public async Task<int> GetPointsAsync(string customerId) =>
            await _db.Customers.Where(c => c.Id == customerId).Select(c => c.LoyaltyPoints).FirstOrDefaultAsync();

        public async Task<ServiceResult<CustomerVoucher>> RedeemAsync(string customerId, int voucherId)
        {
            var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.Id == voucherId && v.IsActive);
            if (voucher == null) return ServiceResult<CustomerVoucher>.Fail("This reward is no longer available.");

            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer == null) return ServiceResult<CustomerVoucher>.Fail("Customer not found.");

            if (customer.LoyaltyPoints < voucher.PointsCost)
                return ServiceResult<CustomerVoucher>.Fail(
                    $"You need {voucher.PointsCost} points for this reward. You have {customer.LoyaltyPoints}.");

            var code = await NewCodeAsync();
            var bought = new CustomerVoucher
            {
                Code = code,
                CustomerId = customerId,
                VoucherId = voucher.Id,
                PointsSpent = voucher.PointsCost,
                RedeemedAt = DateTime.Now,
                ExpiresAt = DateTime.Today.AddDays(voucher.ValidDays + 1).AddSeconds(-1)   // end of the last day
            };

            customer.LoyaltyPoints -= voucher.PointsCost;
            _db.CustomerVouchers.Add(bought);
            _notifications.Queue(customerId, "Voucher redeemed",
                $"You redeemed \"{voucher.Name}\" for {voucher.PointsCost} points. Use code {code} at checkout before {bought.ExpiresAt:d MMM yyyy}.",
                NotificationType.Promotion, "/CustomerPanel/Rewards");

            await _db.SaveChangesAsync();   // points and voucher saved together
            bought.Voucher = voucher;
            return ServiceResult<CustomerVoucher>.Ok(bought);
        }

        // ------------------------------------------------------------
        // ADMIN: rewards catalogue
        // ------------------------------------------------------------

        public Task<List<Voucher>> GetAllAsync() =>
            _db.Vouchers.AsNoTracking().Include(v => v.MenuItem).OrderBy(v => v.PointsCost).ToListAsync();

        public Task<Voucher?> GetAsync(int id) =>
            _db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);

        public Task<int> CountRedeemedAsync(int voucherId) =>
            _db.CustomerVouchers.CountAsync(cv => cv.VoucherId == voucherId);

        public async Task<ServiceResult<Voucher>> SaveAsync(Voucher input)
        {
            var name = input.Name?.Trim() ?? string.Empty;
            if (name.Length < 3 || name.Length > 80) return ServiceResult<Voucher>.Fail("Name must be 3 to 80 characters.");
            if (input.PointsCost < 1) return ServiceResult<Voucher>.Fail("Points cost must be at least 1.");
            if (input.ValidDays < 1 || input.ValidDays > 365) return ServiceResult<Voucher>.Fail("Valid days must be between 1 and 365.");
            if (input.MinSpend < 0) return ServiceResult<Voucher>.Fail("Minimum spend cannot be negative.");

            switch (input.Type)
            {
                case VoucherType.FixedAmount when input.Value <= 0:
                    return ServiceResult<Voucher>.Fail("Enter the RM amount the voucher takes off.");
                case VoucherType.Percent when input.Value <= 0 || input.Value > 100:
                    return ServiceResult<Voucher>.Fail("Percentage must be more than 0% and at most 100%.");
                case VoucherType.BuyOneFreeOne when input.MenuItemId == null || !await _db.MenuItems.AnyAsync(m => m.Id == input.MenuItemId):
                    return ServiceResult<Voucher>.Fail("Choose the dish for the Buy 1 Free 1 voucher.");
            }

            var image = string.IsNullOrWhiteSpace(input.ImageUrl) ? null : input.ImageUrl.Trim();
            if (image != null && !image.StartsWith("/uploads/") && !Uri.IsWellFormedUriString(image, UriKind.Absolute))
                return ServiceResult<Voucher>.Fail("Image must be an uploaded file or a full link starting with http:// or https://.");

            var voucher = input.Id == 0 ? new Voucher() : await _db.Vouchers.FindAsync(input.Id);
            if (voucher == null) return ServiceResult<Voucher>.Fail("Voucher not found.");
            if (input.Id == 0) _db.Vouchers.Add(voucher);

            // Vouchers customers already bought keep working: they read these settings when used,
            // so a change applies to them too (e.g. a better discount).
            voucher.Name = name;
            voucher.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            voucher.PointsCost = input.PointsCost;
            voucher.Type = input.Type;
            voucher.Value = input.Type == VoucherType.BuyOneFreeOne ? 0 : input.Value;
            voucher.MenuItemId = input.Type == VoucherType.BuyOneFreeOne ? input.MenuItemId : null;
            voucher.MinSpend = input.MinSpend;
            voucher.ValidDays = input.ValidDays;
            voucher.ImageUrl = image;
            voucher.IsActive = input.IsActive;

            await _db.SaveChangesAsync();
            return ServiceResult<Voucher>.Ok(voucher);
        }

        public async Task<ServiceResult> SetActiveAsync(int id, bool active)
        {
            var voucher = await _db.Vouchers.FindAsync(id);
            if (voucher == null) return ServiceResult.Fail("Voucher not found.");
            voucher.IsActive = active;
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // A voucher customers have already bought cannot be deleted (they paid points for it);
        // switch it off instead so nobody new can buy it.
        public async Task<ServiceResult> DeleteAsync(int id)
        {
            var voucher = await _db.Vouchers.FindAsync(id);
            if (voucher == null) return ServiceResult.Fail("Voucher not found.");
            if (await _db.CustomerVouchers.AnyAsync(cv => cv.VoucherId == id))
                return ServiceResult.Fail("Customers have already redeemed this voucher, so it can't be deleted. Switch it off instead.");

            _db.Vouchers.Remove(voucher);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        private async Task<string> NewCodeAsync()
        {
            while (true)
            {
                var code = "ART-" + new string(Enumerable.Range(0, 6)
                    .Select(_ => CodeChars[RandomNumberGenerator.GetInt32(CodeChars.Length)]).ToArray());
                if (!await _db.CustomerVouchers.AnyAsync(cv => cv.Code == code)) return code;
            }
        }
    }
}
