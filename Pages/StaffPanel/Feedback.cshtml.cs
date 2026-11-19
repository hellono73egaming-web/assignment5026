using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.StaffPanel
{
    // Staff g: see what customers are saying (read-only; replies are written by the admin).
    public class FeedbackModel : PageModel
    {
        private readonly IFeedbackService _feedback;

        public FeedbackModel(IFeedbackService feedback)
        {
            _feedback = feedback;
        }

        public List<Feedback> Recent { get; set; } = new();
        public int TodayCount { get; set; }
        public double TodayAverage { get; set; }

        public async Task OnGetAsync()
        {
            var all = await _feedback.GetAllAsync();
            var today = all.Where(f => f.CreatedAt.Date == DateTime.Today).ToList();

            TodayCount = today.Count;
            TodayAverage = today.Count == 0 ? 0 : today.Average(f => f.Rating);
            Recent = all.Take(30).ToList();
        }

        // ---- Helpers used by the page ----
        public static string When(DateTime t)
        {
            var span = DateTime.Now - t;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (t.Date == DateTime.Today) return $"{(int)span.TotalHours} hours ago";
            if (t.Date == DateTime.Today.AddDays(-1)) return $"Yesterday at {t:h:mm tt}";
            return t.ToString("MMM dd, yyyy, h:mm tt");
        }

        public static string Service(DateTime visit) =>
            visit.Hour < 11 ? "Breakfast Service" : visit.Hour < 16 ? "Lunch Service" : "Dinner Service";

        public static string Initial(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[0].ToString().ToUpper();
    }
}
