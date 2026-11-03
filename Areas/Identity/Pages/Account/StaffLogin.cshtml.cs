using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    // AD1, RS1 - Staff Portal login: Admin and Staff ONLY.
    // Email + password only. No Google/Facebook/Microsoft, no registration link.
    // Customers use the separate customer Login page.
    [AllowAnonymous]
    public class StaffLoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StaffLoginModel> _logger;

        public StaffLoginModel(SignInManager<ApplicationUser> signInManager,
                               UserManager<ApplicationUser> userManager,
                               ILogger<StaffLoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Enter a valid email address.")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Keep me logged in")]
            public bool RememberMe { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
            {
                var current = await _userManager.GetUserAsync(User);
                if (current is Admin or Staff) return RedirectToDashboard(current);
            }

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ReturnUrl = returnUrl;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return Page();
            }

            // Only Admin and Staff subclasses may use this page
            if (user is Customer)
            {
                ModelState.AddModelError(string.Empty, "This portal is for staff and administrators. Customers, please use the customer login.");
                return Page();
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact the administrator.");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("{Email} ({Role}) logged in via Staff Portal.", user.Email, user.RoleName);

                if (user is Admin admin)
                {
                    admin.LastLoginAt = DateTime.Now;
                    await _userManager.UpdateAsync(admin);
                }

                if (!string.IsNullOrEmpty(returnUrl) && returnUrl != "/" && Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);

                return RedirectToDashboard(user);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty,
                    "Too many failed attempts. Your account is locked for 5 minutes.");
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }

        private IActionResult RedirectToDashboard(ApplicationUser user) => user switch
        {
            Admin => RedirectToPage("/AdminPanel/Index", new { area = "" }),
            Staff => RedirectToPage("/StaffPanel/Index", new { area = "" }),
            _     => RedirectToPage("/Index", new { area = "" })
        };
    }
}
