using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;

namespace WebApplication2.Pages
{
    // Start page "/": no page of its own, it sends each visitor to the right place.
    // Not logged in -> the Register page; logged in -> their own dashboard by role.
    public class IndexModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToPage("/Account/Register", new { area = "Identity" });

            if (User.IsInRole(Roles.Admin)) return RedirectToPage("/AdminPanel/Index");
            if (User.IsInRole(Roles.Staff)) return RedirectToPage("/StaffPanel/Index");
            return RedirectToPage("/CustomerPanel/Index");
        }
    }
}
