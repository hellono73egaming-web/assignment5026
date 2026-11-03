using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // CS3, CS4, RS2, RS3 - business logic for viewing and editing your own profile.
    // Uses Identity's UserManager to load and save users (no direct SQL).
    public class ProfileService : IProfileService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public const int MinimumCustomerAge = 13;
        private static readonly Regex PhonePattern = new(@"^\+?[0-9\- ]{9,15}$");

        public ProfileService(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // ------------------------------------------------------------
        // VIEW (CS3, RS2)
        // ------------------------------------------------------------
        public async Task<ProfileUser?> GetProfileAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            // Fields every user has (from the ApplicationUser base class)
            var profile = new ProfileUser
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt
            };

            // POLYMORPHISM: add the extra fields of the actual subclass
            switch (user)
            {
                case Customer customer:
                    profile.DiningPreferences = customer.DiningPreferences;
                    profile.DateOfBirth = customer.DateOfBirth;
                    profile.LoyaltyPoints = customer.LoyaltyPoints;
                    break;

                case Staff staff:
                    profile.EmployeeId = staff.EmployeeId;
                    profile.Position = staff.Position;
                    profile.HireDate = staff.HireDate;
                    break;

                case Admin admin:
                    profile.LastLoginAt = admin.LastLoginAt;
                    break;
            }

            return profile;
        }

        // ------------------------------------------------------------
        // EDIT (CS4, RS3)
        // ------------------------------------------------------------
        public async Task<ServiceResult> UpdateProfileAsync(string userId, UpdateProfileRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return ServiceResult.Fail("User not found.");

            var error = Validate(request, user is Customer);
            if (error != null) return ServiceResult.Fail(error);

            // Fields every role may change
            user.FullName = request.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

            // Customer-only fields. Staff/Admin values are ignored even if sent.
            if (user is Customer customer)
            {
                customer.DiningPreferences = string.IsNullOrWhiteSpace(request.DiningPreferences)
                    ? null
                    : request.DiningPreferences.Trim();
                customer.DateOfBirth = request.DateOfBirth?.Date;
            }

            // Email, role, loyalty points, employee ID, position are NEVER changed here.
            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded
                ? ServiceResult.Ok()
                : ServiceResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        // ------------------------------------------------------------
        // PASSWORD
        // ------------------------------------------------------------
        public async Task<bool> HasPasswordAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            return user != null && await _userManager.HasPasswordAsync(user);
        }

        // Normal users: must know their current password
        public async Task<ServiceResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return ServiceResult.Fail("User not found.");

            // Identity checks the current password AND the password rules from Program.cs
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            return result.Succeeded
                ? ServiceResult.Ok()
                : ServiceResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        // Google/Microsoft/Facebook sign-ups have no password yet
        public async Task<ServiceResult> SetPasswordAsync(string userId, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return ServiceResult.Fail("User not found.");

            if (await _userManager.HasPasswordAsync(user))
                return ServiceResult.Fail("You already have a password. Use Change Password instead.");

            var result = await _userManager.AddPasswordAsync(user, newPassword);
            return result.Succeeded
                ? ServiceResult.Ok()
                : ServiceResult.Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        // ------------------------------------------------------------
        // VALIDATION (server-side, so it can't be skipped by the browser)
        // ------------------------------------------------------------
        private static string? Validate(UpdateProfileRequest request, bool isCustomer)
        {
            var name = request.FullName?.Trim() ?? string.Empty;
            if (name.Length < 2 || name.Length > 100)
                return "Full name must be between 2 and 100 characters.";

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !PhonePattern.IsMatch(request.PhoneNumber.Trim()))
                return "Enter a valid phone number, e.g. +60123456789.";

            if (isCustomer)
            {
                if (request.DiningPreferences?.Length > 500)
                    return "Dining preferences can be at most 500 characters.";

                if (request.DateOfBirth != null)
                {
                    var dob = request.DateOfBirth.Value.Date;
                    if (dob > DateTime.Today)
                        return "Date of birth cannot be in the future.";

                    var age = DateTime.Today.Year - dob.Year;
                    if (dob > DateTime.Today.AddYears(-age)) age--;   // birthday not reached yet this year
                    if (age < MinimumCustomerAge)
                        return $"You must be at least {MinimumCustomerAge} years old.";
                }
            }

            return null;   // no errors
        }
    }
}