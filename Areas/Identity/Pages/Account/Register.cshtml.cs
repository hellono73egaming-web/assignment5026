using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    // CS1 - public registration. Always creates a CUSTOMER.
    // Staff and Admin accounts cannot be created here (AD8: admin creates staff).
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly INotificationService _notifications;
        private readonly ILogger<RegisterModel> _logger;

        public RegisterModel(UserManager<ApplicationUser> userManager,
                             SignInManager<ApplicationUser> signInManager,
                             INotificationService notifications,
                             ILogger<RegisterModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _notifications = notifications;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Full name is required.")]
            [StringLength(100, MinimumLength = 2)]
            [Display(Name = "Full Name")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Enter a valid email address.")]
            [Display(Name = "Email Address")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Phone number is required.")]
            [RegularExpression(@"^\+?[0-9\- ]{9,15}$", ErrorMessage = "Enter a valid phone number, e.g. +60123456789.")]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please confirm your password.")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm Password")]
            [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            // A bool must be exactly true to pass
            [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the Terms & Conditions.")]
            public bool AgreeToTerms { get; set; }
        }

        public IActionResult OnGet()
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToPage("/Index", new { area = "" });
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Anyone already logged in (Admin, Staff or Customer) cannot register another account
            if (_signInManager.IsSignedIn(User))
                return RedirectToPage("/Index", new { area = "" });

            if (!ModelState.IsValid)
                return Page();

            // Create the SUBCLASS, not the abstract base
            var customer = new Customer
            {
                UserName = Input.Email,
                Email = Input.Email,
                FullName = Input.FullName.Trim(),
                PhoneNumber = Input.PhoneNumber.Trim()
            };

            // Identity hashes the password and enforces the password rules
            var result = await _userManager.CreateAsync(customer, Input.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    // Show one friendly message for duplicate email instead of two
                    if (error.Code == "DuplicateUserName") continue;
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            await _userManager.AddToRoleAsync(customer, customer.RoleName);

            // CS19-22: first in-system notification
            await _notifications.SendAsync(customer.Id, "Welcome to Artie's!",
                $"Hi {customer.FullName}, your account is ready. You can now reserve tables and order online.",
                NotificationType.General);

            _logger.LogInformation("New customer registered: {Email}", customer.Email);

            await _signInManager.SignInAsync(customer, isPersistent: false);
            return RedirectToPage("/CustomerPanel/Index", new { area = "" });
        }
    }
}
