using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // Admin dashboard (Advanced 3): live stats, revenue chart, activity feed, top sellers.
    // Presentation tier only: all figures come from ReportService.
    public class IndexModel : PageModel
    {
        private readonly IReportService _reports;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(IReportService reports, UserManager<ApplicationUser> userManager)
        {
            _reports = reports;
            _userManager = userManager;
        }

        public string AdminName { get; set; } = "Admin";
        public AdminDashboard Data { get; set; } = null!;
        public string ChartJson { get; set; } = "{}";

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            AdminName = user?.FullName ?? "Admin";

            Data = await _reports.GetDashboardAsync();

            // Chart data for the three filter options (JSON is HTML-escaped by default)
            ChartJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["7days"] = new { labels = Data.Last7Days.Labels, data = Data.Last7Days.Values },
                ["30days"] = new { labels = Data.Last30Days.Labels, data = Data.Last30Days.Values },
                ["year"] = new { labels = Data.ThisYear.Labels, data = Data.ThisYear.Values }
            });
        }

        // ---- Helpers used by the page ----
        public double? ChangeVsYesterday =>
            Data.YesterdayRevenue == 0 ? null : (double)((Data.TodayRevenue - Data.YesterdayRevenue) / Data.YesterdayRevenue * 100);

        public static string Ago(DateTime t)
        {
            var span = DateTime.Now - t;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
            if (span.TotalDays < 2) return "Yesterday";
            return t.ToString("d MMM yyyy");
        }
    }
}
