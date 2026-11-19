using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // One customer row on the Users page, with their activity totals
    public record CustomerSummary(Customer Customer, int Orders, decimal Spent, int Bookings);

    // Business tier for the admin Users page. Uses ASP.NET Core Identity's UserManager
    // so passwords are hashed and roles are assigned the same way as at registration.
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        // Job titles a staff member can have (stored in Staff.Position; the login role is always "Staff")
        public static readonly string[] Positions = { "Waiter", "Chef", "Cashier", "Host", "Manager" };

        private readonly INotificationService _notifications;

        public UserService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, INotificationService notifications)
        {
            _db = db;
            _userManager = userManager;
            _notifications = notifications;
        }

        public async Task<List<CustomerSummary>> GetCustomersAsync()
        {
            var customers = await _db.Customers.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync();

            var orderTotals = await _db.Orders
                .Where(o => o.Status != OrderStatus.Cancelled)
                .GroupBy(o => o.CustomerId)
                .Select(g => new { g.Key, Count = g.Count(), Spent = g.Sum(o => o.Total) })
                .ToDictionaryAsync(x => x.Key);

            var bookingCounts = await _db.Reservations
                .Where(r => r.Status != ReservationStatus.Cancelled)
                .GroupBy(r => r.CustomerId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return customers.Select(c => new CustomerSummary(c,
                orderTotals.TryGetValue(c.Id, out var o) ? o.Count : 0,
                orderTotals.TryGetValue(c.Id, out var o2) ? o2.Spent : 0,
                bookingCounts.GetValueOrDefault(c.Id))).ToList();
        }

        public Task<List<Staff>> GetStaffAsync() =>
            _db.StaffMembers.AsNoTracking().OrderBy(s => s.EmployeeId).ToListAsync();

        // Admin registers a staff account (staff cannot self-register)
        public async Task<ServiceResult> CreateStaffAsync(string fullName, string email, string? phone, string position, string password)
        {
            var error = ValidateStaff(fullName, position);
            if (error != null) return ServiceResult.Fail(error);
            if (string.IsNullOrWhiteSpace(email)) return ServiceResult.Fail("Email is required.");

            var staff = new Staff
            {
                UserName = email.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true,
                FullName = fullName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
                Position = position,
                EmployeeId = await NextEmployeeIdAsync(),
                HireDate = DateTime.Today
            };

            // UserManager checks unique email + password rules and hashes the password
            var result = await _userManager.CreateAsync(staff, password ?? string.Empty);
            if (!result.Succeeded)
                return ServiceResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(staff, Roles.Staff);
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> UpdateStaffAsync(string id, string fullName, string? phone, string position)
        {
            var error = ValidateStaff(fullName, position);
            if (error != null) return ServiceResult.Fail(error);

            if (await _userManager.FindByIdAsync(id) is not Staff staff)
                return ServiceResult.Fail("Staff account not found.");

            staff.FullName = fullName.Trim();
            staff.PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            staff.Position = position;

            var result = await _userManager.UpdateAsync(staff);
            return result.Succeeded ? ServiceResult.Ok()
                                    : ServiceResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        // Suspend / re-activate. Login pages already refuse accounts with IsActive = false.
        public async Task<ServiceResult> SetActiveAsync(string id, bool active, string currentUserId)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return ServiceResult.Fail("Account not found.");
            if (user.Id == currentUserId) return ServiceResult.Fail("You cannot suspend your own account.");
            if (user is Admin) return ServiceResult.Fail("Administrator accounts cannot be suspended here.");

            user.IsActive = active;
            await _userManager.UpdateAsync(user);

            // Changing the security stamp signs a suspended user out of any open sessions
            if (!active) await _userManager.UpdateSecurityStampAsync(user);
            return ServiceResult.Ok();
        }

        // Forgot password: the customer contacts the restaurant and the admin resets it here.
        public async Task<ServiceResult<string>> ResetCustomerPasswordAsync(string customerId)
        {
            var user = await _userManager.FindByIdAsync(customerId);
            if (user is not Customer customer) return ServiceResult<string>.Fail("Customer not found.");

            // e.g. "Gabagool#4821": passes every password rule (upper, lower, number, symbol, 8+ characters)
            var temporary = $"Gabagool#{RandomNumberGenerator.GetInt32(1000, 10000)}";

            // Identity's own reset token: checks the password rules and stores only the hash
            var token = await _userManager.GeneratePasswordResetTokenAsync(customer);
            var result = await _userManager.ResetPasswordAsync(customer, token, temporary);
            if (!result.Succeeded)
                return ServiceResult<string>.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

            // A customer locked out by wrong guesses can log in straight away
            await _userManager.SetLockoutEndDateAsync(customer, null);
            await _userManager.ResetAccessFailedCountAsync(customer);

            await _notifications.SendAsync(customer.Id, "Password reset by Artie's",
                "Your password was reset by the restaurant. Please change your temporary password on My Profile.",
                NotificationType.General, "/Profile");

            return ServiceResult<string>.Ok(temporary);
        }

        // Delete only accounts with no history; otherwise suspend (keeps sales records correct)
        public async Task<ServiceResult> DeleteAsync(string id, string currentUserId)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return ServiceResult.Fail("Account not found.");
            if (user.Id == currentUserId) return ServiceResult.Fail("You cannot delete your own account.");
            if (user is Admin) return ServiceResult.Fail("Administrator accounts cannot be deleted here.");

            if (user is Customer)
            {
                var hasHistory = await _db.Orders.AnyAsync(o => o.CustomerId == id)
                              || await _db.Reservations.AnyAsync(r => r.CustomerId == id)
                              || await _db.Feedbacks.AnyAsync(f => f.CustomerId == id);
                if (hasHistory)
                    return ServiceResult.Fail("This customer has orders or bookings on record. Suspend the account instead.");
            }

            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded ? ServiceResult.Ok()
                                    : ServiceResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        // ---- helpers ----
        private static string? ValidateStaff(string fullName, string position)
        {
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2 || fullName.Trim().Length > 100)
                return "Full name must be between 2 and 100 characters.";
            if (!Positions.Contains(position))
                return "Please choose a valid role.";
            return null;
        }

        // EMP001, EMP002, ... (skips numbers already used)
        private async Task<string> NextEmployeeIdAsync()
        {
            var used = await _db.StaffMembers.Select(s => s.EmployeeId).ToListAsync();
            var n = used.Count + 1;
            while (used.Contains($"EMP{n:D3}")) n++;
            return $"EMP{n:D3}";
        }
    }
}
