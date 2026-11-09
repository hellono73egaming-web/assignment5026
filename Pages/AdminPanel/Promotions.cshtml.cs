using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // AD6 (promotional offers), AD16 (campaigns), AD15 (announcements), CS21 (promotion notifications).
    // Presentation tier only: validation and saving happen in PromotionService / AnnouncementService.
    public class PromotionsModel : PageModel
    {
        private readonly IPromotionService _promotions;
        private readonly IAnnouncementService _announcements;
        private readonly IMenuService _menu;

        public PromotionsModel(IPromotionService promotions, IAnnouncementService announcements, IMenuService menu)
        {
            _promotions = promotions;
            _announcements = announcements;
            _menu = menu;
        }

        // ---- What the page shows ----
        public List<PromotionRow> Promotions { get; set; } = new();
        public List<AnnouncementRow> Announcements { get; set; } = new();
        public List<MenuCategory> Categories { get; set; } = new();

        [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            Categories = await _menu.GetMenuAsync();
            Promotions = (await _promotions.GetAllAsync()).Select(PromotionRow.From).ToList();
            Announcements = (await _announcements.GetAllAsync()).Select(AnnouncementRow.From).ToList();
        }

        // ---- Promotions ----
        public async Task<IActionResult> OnPostSavePromoAsync(PromoInput promo, bool notify)
        {
            var isFixed = promo.DiscountType == "Fixed";
            var existing = promo.Id == 0 ? null : await _promotions.GetAsync(promo.Id);

            var entity = new Promotion
            {
                Id = promo.Id,
                Title = promo.Title ?? string.Empty,
                Description = promo.Description,
                DiscountType = isFixed ? PromotionDiscountType.Fixed : PromotionDiscountType.Percent,
                DiscountPercent = isFixed ? 0 : promo.DiscountValue,
                DiscountAmount = isFixed ? promo.DiscountValue : 0,
                PromoCode = promo.PromoCode,
                MenuCategoryId = promo.CategoryId,
                MenuItemId = existing?.MenuItemId,          // keep an item-level scope set elsewhere
                StartDate = promo.StartDate,
                EndDate = promo.EndDate,
                IsActive = promo.IsActive
            };

            var result = await _promotions.SaveAsync(entity, notify && promo.Id == 0);
            if (result.Success)
                Message = promo.Id == 0
                    ? $"Promotion \"{entity.Title}\" created" + (notify ? " and sent to customers." : ".")
                    : $"Promotion \"{entity.Title}\" updated.";
            else Error = result.Error;

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostTogglePromoAsync(int id)
        {
            var p = await _promotions.GetAsync(id);
            if (p == null) { Error = "Promotion not found."; return RedirectToPage(); }

            var result = await _promotions.SetActiveAsync(id, !p.IsActive);
            if (result.Success) Message = p.IsActive ? $"\"{p.Title}\" deactivated." : $"\"{p.Title}\" activated.";
            else Error = result.Error;
            return RedirectToPage();
        }

        // ---- Announcements ----
        public async Task<IActionResult> OnPostSaveAnnouncementAsync(AnnouncementInput announcement, bool sendNotification)
        {
            var entity = new Announcement
            {
                Id = announcement.Id,
                Title = announcement.Title ?? string.Empty,
                Content = announcement.Message ?? string.Empty,
                StartDate = announcement.PublishDate,
                IsActive = announcement.IsActive,
                Audience = Enum.TryParse<AnnouncementAudience>(announcement.Audience, out var aud) ? aud : AnnouncementAudience.Customers
            };

            // Editing keeps the original end date
            if (announcement.Id != 0)
                entity.EndDate = (await _announcements.GetAllAsync()).FirstOrDefault(a => a.Id == announcement.Id)?.EndDate;

            var result = await _announcements.SaveAsync(entity, sendNotification && announcement.Id == 0);
            if (result.Success)
                Message = announcement.Id == 0
                    ? $"Announcement \"{entity.Title}\" published" + (sendNotification ? " and sent as a notification." : ".")
                    : $"Announcement \"{entity.Title}\" updated.";
            else Error = result.Error;

            return RedirectToPage(new { tab = "announcements" });
        }

        public async Task<IActionResult> OnPostToggleAnnouncementAsync(int id)
        {
            var a = (await _announcements.GetAllAsync()).FirstOrDefault(x => x.Id == id);
            if (a == null) { Error = "Announcement not found."; return RedirectToPage(new { tab = "announcements" }); }

            var result = await _announcements.SetActiveAsync(id, !a.IsActive);
            if (result.Success) Message = a.IsActive ? $"\"{a.Title}\" deactivated." : $"\"{a.Title}\" activated.";
            else Error = result.Error;
            return RedirectToPage(new { tab = "announcements" });
        }

        // ---- Form inputs (names match the page: promo.Title, announcement.Message, ...) ----
        public class PromoInput
        {
            public int Id { get; set; }
            public string? Title { get; set; }
            public string? Description { get; set; }
            public string DiscountType { get; set; } = "Percent";   // "Percent" or "Fixed"
            public decimal DiscountValue { get; set; }
            public string? PromoCode { get; set; }
            public int? CategoryId { get; set; }
            public DateTime StartDate { get; set; } = DateTime.Today;
            public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);
            public bool IsActive { get; set; }
        }

        public class AnnouncementInput
        {
            public int Id { get; set; }
            public string? Title { get; set; }
            public string? Message { get; set; }
            public string Audience { get; set; } = "Customers";
            public DateTime PublishDate { get; set; } = DateTime.Today;
            public bool IsActive { get; set; }
        }

        // ---- Rows shown in the tables ----
        public class PromotionRow
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string DiscountType { get; set; } = "Percent";
            public decimal DiscountValue { get; set; }
            public string DiscountDisplay { get; set; } = string.Empty;
            public string? PromoCode { get; set; }
            public int? CategoryId { get; set; }
            public string Scope { get; set; } = "All menu items";
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public bool IsActive { get; set; }
            public string Status { get; set; } = string.Empty;
            public string StatusCss { get; set; } = string.Empty;

            public static PromotionRow From(Promotion p)
            {
                var today = DateTime.Today;
                var (status, css) = !p.IsActive ? ("Inactive", "badge-inactive")
                                  : p.EndDate.Date < today ? ("Expired", "badge-expired")
                                  : p.StartDate.Date > today ? ("Scheduled", "badge-scheduled")
                                  : ("Active", "badge-active");
                var isFixed = p.DiscountType == PromotionDiscountType.Fixed;
                return new PromotionRow
                {
                    Id = p.Id,
                    Title = p.Title,
                    Description = p.Description,
                    DiscountType = isFixed ? "Fixed" : "Percent",
                    DiscountValue = isFixed ? p.DiscountAmount : p.DiscountPercent,
                    DiscountDisplay = isFixed ? $"RM {p.DiscountAmount:N2}" : $"{p.DiscountPercent:0.##}%",
                    PromoCode = p.PromoCode,
                    CategoryId = p.MenuCategoryId,
                    Scope = p.MenuItem?.Name ?? p.MenuCategory?.Name ?? "All menu items",
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    IsActive = p.IsActive,
                    Status = status,
                    StatusCss = css
                };
            }
        }

        public class AnnouncementRow
        {
            public int Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string Audience { get; set; } = "Customers";
            public DateTime PublishDate { get; set; }
            public bool IsActive { get; set; }

            public static AnnouncementRow From(Announcement a) => new()
            {
                Id = a.Id,
                Title = a.Title,
                Message = a.Content,
                Audience = a.Audience.ToString(),
                PublishDate = a.StartDate,
                IsActive = a.IsActive
            };
        }
    }
}
