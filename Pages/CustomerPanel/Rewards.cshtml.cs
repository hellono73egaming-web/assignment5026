using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.CustomerPanel
{
    // Rewards: spend loyalty points (1 point per RM 1 paid) on vouchers,
    // then enter the voucher code at checkout.
    public class RewardsModel : PageModel
    {
        private readonly IRewardService _rewards;
        private readonly UserManager<ApplicationUser> _userManager;

        public RewardsModel(IRewardService rewards, UserManager<ApplicationUser> userManager)
        {
            _rewards = rewards;
            _userManager = userManager;
        }

        public int Points { get; set; }
        public List<Voucher> Catalogue { get; set; } = new();
        public List<CustomerVoucher> MyVouchers { get; set; } = new();

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            Points = await _rewards.GetPointsAsync(userId);
            Catalogue = await _rewards.GetCatalogueAsync();
            MyVouchers = await _rewards.GetForCustomerAsync(userId);
        }

        public async Task<IActionResult> OnPostRedeemAsync(int voucherId)
        {
            var result = await _rewards.RedeemAsync(_userManager.GetUserId(User)!, voucherId);
            if (result.Success)
                Message = $"Redeemed! Your code is {result.Value!.Code}. Enter it at checkout.";
            else
                Error = result.Error;
            return RedirectToPage();
        }
    }
}
