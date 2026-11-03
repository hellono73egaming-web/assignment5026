using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages
{
    // CS3, CS4, RS2, RS3 - one profile page for all logged-in users (Admin, Staff, Customer).
    // Presentation tier only: all rules live in IProfileService.
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly IProfileService _profiles;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public ProfileModel(IProfileService profiles,
                            UserManager<ApplicationUser> userManager,
                            SignInManager<ApplicationUser> signInManager)
        {
            _profiles = profiles;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        // Display-only values for the summary card
        public string UserRole { get; set; } = string.Empty;
        public DateTime MemberSince { get; set; }
        public bool HasPassword { get; set; }
        public string DashboardPage { get; set; } = "/Index";

        public class InputModel
        {
            [Required(ErrorMessage = "Full name is required.")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be 2-100 characters.")]
            [Display(Name = "Full name")]
            public string FullName { get; set; } = string.Empty;

            [RegularExpression(@"^\+?[0-9\- ]{9,15}$", ErrorMessage = "Enter a valid phone number, e.g. +60123456789.")]
            [Display(Name = "Phone number")]
            public string? PhoneNumber { get; set; }

            // Shown read-only. Never saved from the form.
            [Display(Name = "Email address")]
            public string? Email { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string? CurrentPassword { get; set; }

            [DataType(DataType.Password)]
            [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
            [Display(Name = "New password")]
            public string? NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
            [Display(Name = "Confirm new password")]
            public string? ConfirmNewPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var profile = await LoadProfileAsync();
            if (profile == null) return Challenge();

            Input.FullName = profile.FullName;
            Input.PhoneNumber = profile.PhoneNumber;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // The user ID ALWAYS comes from the login cookie, never from the form (prevents editing someone else)
            var userId = _userManager.GetUserId(User);
            var profile = await LoadProfileAsync();
            if (userId == null || profile == null) return Challenge();

            var changingPassword = !string.IsNullOrEmpty(Input.NewPassword);

            if (changingPassword && HasPassword && string.IsNullOrEmpty(Input.CurrentPassword))
                ModelState.AddModelError("Input.CurrentPassword", "Enter your current password to set a new one.");

            if (!ModelState.IsValid)
                return Page();

            // 1. Personal details. Customer-only fields this page doesn't edit are passed through unchanged.
            var update = await _profiles.UpdateProfileAsync(userId, new UpdateProfileRequest
            {
                FullName = Input.FullName,
                PhoneNumber = Input.PhoneNumber,
                DiningPreferences = profile.DiningPreferences,
                DateOfBirth = profile.DateOfBirth
            });

            if (!update.Success)
            {
                ModelState.AddModelError(string.Empty, update.Error!);
                return Page();
            }

            // 2. Password (only if the user typed a new one)
            if (changingPassword)
            {
                var pw = HasPassword
                    ? await _profiles.ChangePasswordAsync(userId, Input.CurrentPassword!, Input.NewPassword!)
                    : await _profiles.SetPasswordAsync(userId, Input.NewPassword!);

                if (!pw.Success)
                {
                    ModelState.AddModelError(string.Empty, pw.Error!);
                    return Page();
                }

                // Keep the user logged in after the security stamp changes
                var user = await _userManager.GetUserAsync(User);
                if (user != null) await _signInManager.RefreshSignInAsync(user);
            }

            TempData["SuccessMessage"] = changingPassword
                ? "Your profile and password have been updated."
                : "Your profile has been updated.";

            // Post/Redirect/Get: refreshing the page won't resubmit the form
            return RedirectToPage();
        }

        // Loads the read-only summary values (used by both GET and POST)
        private async Task<ProfileUser?> LoadProfileAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return null;

            var profile = await _profiles.GetProfileAsync(userId);
            if (profile == null) return null;

            Input.Email = profile.Email;   // always from the database, not the form
            UserRole = profile.RoleName;
            MemberSince = profile.CreatedAt;
            HasPassword = await _profiles.HasPasswordAsync(userId);
            DashboardPage = profile.IsAdmin ? "/AdminPanel/Index"
                          : profile.IsStaff ? "/StaffPanel/Index"
                          : "/CustomerPanel/Index";
            return profile;
        }
    }
}
