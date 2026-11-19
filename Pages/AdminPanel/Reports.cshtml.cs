using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Services;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.AdminPanel
{
    // Admin f / Intermediate 5-6 / Advanced 2-4:
    // filtered reports (sales, reservations, popular items, customer activity),
    // date filter (today / week / month / year), charts and CSV export.
    public class ReportsModel : PageModel
    {
        private readonly IReportService _reports;

        public ReportsModel(IReportService reports)
        {
            _reports = reports;
        }

        [BindProperty(SupportsGet = true)]
        public ReportType Type { get; set; } = ReportType.Sales;

        [BindProperty(SupportsGet = true)]
        public ReportRange Range { get; set; } = ReportRange.Week;

        public ReportResult Report { get; set; } = new();
        public string ChartJson { get; set; } = "{}";

        public async Task OnGetAsync()
        {
            Report = await _reports.GetReportAsync(Type, Range);

            ChartJson = JsonSerializer.Serialize(new
            {
                trend = new { labels = Report.Trend.Labels, data = Report.Trend.Values, label = Report.TrendLabel, money = Report.TrendIsMoney },
                breakdown = new { labels = Report.Breakdown.Labels, data = Report.Breakdown.Values, type = Report.BreakdownChart }
            });
        }

        // GET ?handler=Export&type=Sales&range=Week  -> downloads the same report as a CSV file
        public async Task<IActionResult> OnGetExportAsync()
        {
            var report = await _reports.GetReportAsync(Type, Range);
            var csv = _reports.ToCsv(report);

            // UTF-8 with BOM so Excel opens "RM" and names correctly
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return File(bytes, "text/csv", $"rroms-{Type}-{Range}-{DateTime.Today:yyyyMMdd}.csv".ToLower());
        }

        // ---- Helpers used by the page ----
        public string CompareLabel => Range switch
        {
            ReportRange.Today => "vs yesterday",
            ReportRange.Month => "vs last month",
            ReportRange.Year => "vs last year",
            _ => "vs last week"
        };

        // % change from the previous period (null when there is nothing to compare with)
        public static double? Change(decimal current, decimal previous) =>
            previous == 0 ? null : (double)((current - previous) / previous * 100);

        public static string TypeLabel(ReportType t) => t switch
        {
            ReportType.Reservations => "Reservation Statistics",
            ReportType.Popular => "Popular Menu Items",
            ReportType.Customer => "Customer Activity",
            _ => "Sales Performance"
        };
    }
}
