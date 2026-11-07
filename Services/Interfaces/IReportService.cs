namespace WebApplication2.Services.Interfaces
{
    // AD6 / Admin f, Intermediate 5-6, Advanced 2-4: sales reporting and business analytics.
    // All the number crunching lives here so the Razor Pages only display the results.
    public interface IReportService
    {
        Task<AdminDashboard> GetDashboardAsync();
        Task<ReportResult> GetReportAsync(ReportType type, ReportRange range);
        string ToCsv(ReportResult report);
    }
}
