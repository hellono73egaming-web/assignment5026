using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // Admin c: manage customer and staff accounts (register staff, assign role, suspend, delete).
    public class UsersModel : PageModel
    {
        private readonly IUserService _users;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersModel(IUserService users, UserManager<ApplicationUser> userManager)
        {
            _users = users;
            _userManager = userManager;
        }

        public List<CustomerSummary> Customers { get; set; } = new();
        public List<Staff> StaffMembers { get; set; } = new();

        // Which tab to open after a redirect ("customers" or "staff")
        [BindProperty(SupportsGet = true)]
        public string? Tab { get; set; }

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        // Shown once after a password reset, so the admin can read it to the customer
        [TempData] public string? TempPassword { get; set; }
        [TempData] public string? TempPasswordFor { get; set; }

        public async Task OnGetAsync()
        {
            Customers = await _users.GetCustomersAsync();
            StaffMembers = await _users.GetStaffAsync();
        }

        // One handler for the Add / Edit staff modal: no id = new account
        public async Task<IActionResult> OnPostSaveStaffAsync(string? id, string fullName, string? email,
                                                              string? phone, string position, string? password)
        {
            var result = string.IsNullOrEmpty(id)
                ? await _users.CreateStaffAsync(fullName, email ?? string.Empty, phone, position, password ?? string.Empty)
                : await _users.UpdateStaffAsync(id, fullName, phone, position);

            if (result.Success) Message = string.IsNullOrEmpty(id) ? $"Staff account for {fullName} created." : $"{fullName} updated.";
            else Error = result.Error;

            return RedirectToPage(new { tab = "staff" });
        }

        public async Task<IActionResult> OnPostToggleAsync(string id, bool active, string name, string tab)
        {
            var result = await _users.SetActiveAsync(id, active, _userManager.GetUserId(User)!);

            if (result.Success) Message = active ? $"{name} has been re-activated." : $"{name} has been suspended and can no longer log in.";
            else Error = result.Error;

            return RedirectToPage(new { tab });
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id, string name, string tab)
        {
            var result = await _users.DeleteAsync(id, _userManager.GetUserId(User)!);

            if (result.Success) Message = $"{name}'s account has been deleted.";
            else Error = result.Error;

            return RedirectToPage(new { tab });
        }

        // Forgot password (customer contacted the restaurant): set a temporary password, shown to the admin once
        public async Task<IActionResult> OnPostResetPasswordAsync(string id, string name)
        {
            var result = await _users.ResetCustomerPasswordAsync(id);
            if (result.Success)
            {
                TempPassword = result.Value;
                TempPasswordFor = name;
            }
            else Error = result.Error;

            return RedirectToPage(new { tab = "customers" });
        }

        // ---- Helpers used by the page ----
        public static string Initials(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "?" :
            string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));
    }
}
