using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Areas.Identity.Pages.Account
{
    // CS2 - CUSTOMER login page.
    // Placing this file at Areas/Identity/Pages/Account/Login overrides the built-in Identity login page.
    // Admin and Staff use the separate Staff Portal (StaffLogin page).
    //
    // Two ways to log in:
    //   1. Email + password
    //   2. Google / Facebook / Microsoft
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifications;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<ApplicationUser> signInManager,
                          UserManager<ApplicationUser> userManager,
                          INotificationService notifications,
                          ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _notifications = notifications;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        // Survives a redirect, used to show errors from the Google/Facebook callback
        [TempData]
        public string? ErrorMessage { get; set; }

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
            if (!string.IsNullOrEmpty(ErrorMessage))
                ModelState.AddModelError(string.Empty, ErrorMessage);

            // Someone tried to open an Admin/Staff page while logged out -> send them to the Staff Portal
            if (returnUrl != null &&
                (returnUrl.StartsWith("/AdminPanel", StringComparison.OrdinalIgnoreCase) ||
                 returnUrl.StartsWith("/StaffPanel", StringComparison.OrdinalIgnoreCase)))
            {
                return RedirectToPage("./StaffLogin", new { returnUrl });
            }

            // Already logged in? Go straight to the right dashboard.
            if (_signInManager.IsSignedIn(User))
            {
                var current = await _userManager.GetUserAsync(User);
                if (current is Customer) return RedirectToDashboard(current);
            }

            // Clear any leftover external login cookie
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ReturnUrl = returnUrl;
            return Page();
        }

        // ---------------------------------------------------------------
        // 1. EMAIL + PASSWORD LOGIN (all roles)
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // Same message for "no such user" and "wrong password" so attackers
            // can't find out which emails are registered.
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return Page();
            }

            // Admin and Staff must use the Staff Portal
            if (user is not Customer)
            {
                ModelState.AddModelError(string.Empty, "Staff and administrators, please sign in through the Staff Portal.");
                return Page();
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact the restaurant.");
                return Page();
            }

            // lockoutOnFailure: true -> account locks after 5 wrong attempts (set in Program.cs)
            var result = await _signInManager.PasswordSignInAsync(
                user, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("{Email} ({Role}) logged in.", user.Email, user.RoleName);

                return RedirectAfterLogin(user, returnUrl);
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

        // ---------------------------------------------------------------
        // 2a. GOOGLE / FACEBOOK - send the user to the provider's sign-in page
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnPostExternalLoginAsync(string provider, string? returnUrl = null)
        {
            // Only providers whose keys are configured in Program.cs are registered
            var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
            if (!schemes.Any(s => s.Name == provider))
            {
                ErrorMessage = $"{provider} sign-in is not available yet. Please use your email and password.";
                return RedirectToPage(new { returnUrl });
            }

            var callbackUrl = Url.Page("./Login", pageHandler: "ExternalCallback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, callbackUrl);
            return new ChallengeResult(provider, properties);
        }

        // ---------------------------------------------------------------
        // 2b. GOOGLE / FACEBOOK - the provider sends the user back here
        // ---------------------------------------------------------------
        public async Task<IActionResult> OnGetExternalCallbackAsync(string? returnUrl = null, string? remoteError = null)
        {
            if (remoteError != null)
                return ExternalError($"Sign-in was cancelled or failed: {remoteError}");

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
                return ExternalError("Could not read your details from the provider. Please try again.");

            // A) This Google/Facebook account was linked before -> sign in directly
            var linkedUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (linkedUser != null)
            {
                if (linkedUser is not Customer)
                    return ExternalError("Staff and administrators, please sign in through the Staff Portal.");
                if (!linkedUser.IsActive)
                    return ExternalError("This account has been deactivated. Please contact the restaurant.");

                var result = await _signInManager.ExternalLoginSignInAsync(
                    info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

                if (result.IsLockedOut)
                    return ExternalError("Your account is locked. Please try again later.");
                if (result.Succeeded)
                    return RedirectAfterLogin(linkedUser, returnUrl);

                return ExternalError("Sign-in failed. Please try again.");
            }

            // B) First time with this Google/Facebook account -> we need their email
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email))
                return ExternalError($"{info.ProviderDisplayName} did not share your email address. Please register with email instead.");

            var existingUser = await _userManager.FindByEmailAsync(email);

            // Staff / Admin emails are never allowed to use social login
            if (existingUser != null && existingUser is not Customer)
                return ExternalError("Staff and administrators, please sign in through the Staff Portal.");

            Customer customer;

            if (existingUser is Customer existingCustomer)
            {
                // C) Customer already registered with this email -> link the provider to that account
                if (!existingCustomer.IsActive)
                    return ExternalError("This account has been deactivated. Please contact the restaurant.");
                customer = existingCustomer;
            }
            else
            {
                // D) Brand new person -> create a Customer account (no password needed)
                customer = new Customer
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,   // the provider already verified it
                    FullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email
                };

                var create = await _userManager.CreateAsync(customer);
                if (!create.Succeeded)
                    return ExternalError(string.Join(" ", create.Errors.Select(e => e.Description)));

                await _userManager.AddToRoleAsync(customer, customer.RoleName);

                await _notifications.SendAsync(customer.Id, "Welcome to Artie's!",
                    $"Hi {customer.FullName}, your account was created with {info.ProviderDisplayName}.",
                    NotificationType.General);

                _logger.LogInformation("New customer created via {Provider}: {Email}", info.LoginProvider, email);
            }

            // Remember this Google/Facebook account for next time
            var link = await _userManager.AddLoginAsync(customer, info);
            if (!link.Succeeded)
                return ExternalError(string.Join(" ", link.Errors.Select(e => e.Description)));

            await _signInManager.SignInAsync(customer, isPersistent: false, info.LoginProvider);
            return RedirectAfterLogin(customer, returnUrl);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private IActionResult ExternalError(string message)
        {
            ErrorMessage = message;
            return RedirectToPage();
        }

        private IActionResult RedirectAfterLogin(ApplicationUser user, string? returnUrl)
        {
            // If they were sent here from a protected page, go back to it
            if (!string.IsNullOrEmpty(returnUrl) && returnUrl != "/" && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToDashboard(user);
        }

        // POLYMORPHISM: the actual subclass of the logged-in user decides where they go.
        private IActionResult RedirectToDashboard(ApplicationUser user) => user switch
        {
            Admin    => RedirectToPage("/AdminPanel/Index", new { area = "" }),
            Staff    => RedirectToPage("/StaffPanel/Index", new { area = "" }),
            Customer => RedirectToPage("/CustomerPanel/Index", new { area = "" }),
            _        => RedirectToPage("/Index", new { area = "" })
        };
    }
}
