using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // Review management: rating summary, filter/search, and replying to customers.
    public class ReviewsModel : PageModel
    {
        private readonly IFeedbackService _feedback;

        public ReviewsModel(IFeedbackService feedback)
        {
            _feedback = feedback;
        }

        public List<Feedback> Reviews { get; set; } = new();
        public double Average { get; set; }
        public Dictionary<int, int> CountByStar { get; set; } = new();

        [TempData] public string? Message { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            Reviews = await _feedback.GetAllAsync();
            Average = await _feedback.GetAverageRatingAsync();
            CountByStar = Enumerable.Range(1, 5).ToDictionary(s => s, s => Reviews.Count(r => r.Rating == s));
        }

        public async Task<IActionResult> OnPostReplyAsync(int id, string reply)
        {
            var result = await _feedback.ReplyAsync(id, reply);
            if (result.Success) Message = "Reply posted. The customer has been notified.";
            else Error = result.Error;
            return RedirectToPage();
        }

        // ---- Helpers used by the page ----
        // Simple sentiment from the star rating: 4-5 positive, 3 neutral, 1-2 negative
        public static (string Label, string Css) Sentiment(int rating) =>
            rating >= 4 ? ("Positive", "sent-positive") : rating == 3 ? ("Neutral", "sent-neutral") : ("Negative", "sent-negative");

        public static string Initials(string? name) =>
            string.IsNullOrWhiteSpace(name) ? "?" :
            string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpper(p[0])));
    }
}
