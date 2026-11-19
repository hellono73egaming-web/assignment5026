using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // ---------------- Report options ----------------
    public enum ReportType { Sales, Reservations, Popular, Customer }
    public enum ReportRange { Today, Week, Month, Year }

    // ---------------- Data shapes returned to the pages ----------------
    public record ChartSeries(List<string> Labels, List<decimal> Values);
    public record ActivityItem(string Icon, string Css, string Text, string Meta, DateTime When);
    public record TopItem(string Name, string? ImageUrl, int Quantity, decimal Revenue);

    public record AdminDashboard(
        decimal TodayRevenue, decimal YesterdayRevenue,
        int ActiveOrders, int PreparingOrders,
        int TodayBookings, int TodayGuests,
        int PendingReservations, double AverageRating, int ReviewCount,
        ChartSeries Last7Days, ChartSeries Last30Days, ChartSeries ThisYear,
        List<ActivityItem> Activity, List<TopItem> TopItems);

    public record TableUsage(string Table, string Area, int Capacity, int Bookings, int Guests, double HoursBooked, double UtilisationPercent);

    public class ReportResult
    {
        public ReportType Type { get; set; }
        public ReportRange Range { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }          // exclusive
        public string RangeLabel { get; set; } = string.Empty;

        // KPI cards (+ previous period of the same length, for the % change)
        public decimal Revenue { get; set; }
        public int Orders { get; set; }
        public decimal AverageOrder { get; set; }
        public int Reservations { get; set; }
        public decimal PrevRevenue { get; set; }
        public int PrevOrders { get; set; }
        public decimal PrevAverageOrder { get; set; }
        public int PrevReservations { get; set; }

        // Charts
        public string TrendTitle { get; set; } = string.Empty;
        public string TrendLabel { get; set; } = string.Empty;
        public bool TrendIsMoney { get; set; }
        public ChartSeries Trend { get; set; } = new(new(), new());
        public string BreakdownTitle { get; set; } = string.Empty;
        public string BreakdownChart { get; set; } = "doughnut";   // or "bar"
        public ChartSeries Breakdown { get; set; } = new(new(), new());

        // Detail table (also used for the CSV export)
        public List<string> Columns { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();

        // Reservations report only: table utilisation (Advanced 2)
        public List<TableUsage> TableUsage { get; set; } = new();
    }

    // Business tier for the admin dashboard and reports.
    // Loads the orders / reservations for a period once, then groups them in memory with LINQ.
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _db;
        private static readonly CultureInfo Money = CultureInfo.InvariantCulture;

        public ReportService(ApplicationDbContext db)
        {
            _db = db;
        }

        // =========================================================
        // DASHBOARD
        // =========================================================
        public async Task<AdminDashboard> GetDashboardAsync()
        {
            var today = DateTime.Today;
            var yearStart = new DateTime(today.Year, 1, 1);
            var from = today.AddDays(-29) < yearStart ? today.AddDays(-29) : yearStart;

            // Everything needed for the charts in one query (since the earlier of Jan 1 / 30 days ago)
            var orders = await _db.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems).ThenInclude(i => i.MenuItem)
                .Where(o => o.CreatedAt >= from && o.Status != OrderStatus.Cancelled)
                .AsNoTracking()
                .ToListAsync();

            decimal SalesOn(DateTime day) => orders.Where(o => o.CreatedAt.Date == day).Sum(o => o.Total);

            var last7 = Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6)).ToList();
            var last30 = Enumerable.Range(0, 30).Select(i => today.AddDays(i - 29)).ToList();
            var months = Enumerable.Range(1, 12).ToList();

            var chart7 = new ChartSeries(last7.Select(d => d.ToString("ddd")).ToList(), last7.Select(SalesOn).ToList());
            var chart30 = new ChartSeries(last30.Select(d => d.ToString("d MMM")).ToList(), last30.Select(SalesOn).ToList());
            var chartYear = new ChartSeries(
                months.Select(m => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m)).ToList(),
                months.Select(m => orders.Where(o => o.CreatedAt.Year == today.Year && o.CreatedAt.Month == m).Sum(o => o.Total)).ToList());

            var activeOrders = await _db.Orders.CountAsync(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
            var preparing = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Preparing);

            var todayBookings = await _db.Reservations
                .Where(r => r.StartTime >= today && r.StartTime < today.AddDays(1)
                            && r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.NoShow)
                .Select(r => r.PartySize)
                .ToListAsync();
            var pending = await _db.Reservations.CountAsync(r => r.Status == ReservationStatus.Pending && r.StartTime >= today);

            var reviewCount = await _db.Feedbacks.CountAsync();
            var avgRating = reviewCount == 0 ? 0 : await _db.Feedbacks.AverageAsync(f => f.Rating);

            // Top sellers in the last 30 days
            var topItems = orders.Where(o => o.CreatedAt >= today.AddDays(-29))
                .SelectMany(o => o.OrderItems)
                .GroupBy(i => i.MenuItemId)
                .Select(g => new TopItem(g.First().MenuItem?.Name ?? "Item", g.First().MenuItem?.ImageUrl,
                                         g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal)))
                .OrderByDescending(t => t.Quantity)
                .Take(4)
                .ToList();

            return new AdminDashboard(
                SalesOn(today), SalesOn(today.AddDays(-1)),
                activeOrders, preparing,
                todayBookings.Count, todayBookings.Sum(),
                pending, avgRating, reviewCount,
                chart7, chart30, chartYear,
                await GetActivityAsync(6), topItems);
        }

        // Latest orders, bookings and reviews merged into one feed
        private async Task<List<ActivityItem>> GetActivityAsync(int take)
        {
            var orders = await _db.Orders.Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedAt).Take(take).AsNoTracking().ToListAsync();
            var bookings = await _db.Reservations.Include(r => r.Customer).Include(r => r.RestaurantTable)
                .OrderByDescending(r => r.CreatedAt).Take(take).AsNoTracking().ToListAsync();
            var reviews = await _db.Feedbacks.Include(f => f.Customer)
                .OrderByDescending(f => f.CreatedAt).Take(take).AsNoTracking().ToListAsync();

            var feed = new List<ActivityItem>();

            foreach (var o in orders)
            {
                var (icon, css, verb) = o.Status switch
                {
                    OrderStatus.Completed => ("fa-check-double", "icon-green", "completed"),
                    OrderStatus.Cancelled => ("fa-ban", "icon-danger", "cancelled"),
                    _ => ("fa-utensils", "icon-gold", "placed")
                };
                var when = o.UpdatedAt ?? o.CreatedAt;
                feed.Add(new ActivityItem(icon, css,
                    $"Order #{o.OrderNumber} {verb} by {o.Customer?.FullName}.",
                    $"RM {o.Total.ToString("N2", Money)}", when));
            }

            foreach (var r in bookings)
            {
                feed.Add(new ActivityItem("fa-calendar-check", "icon-blue",
                    $"Table {r.RestaurantTable?.TableNumber} reserved by {r.Customer?.FullName} for {r.StartTime:h:mm tt, d MMM}.",
                    $"Party of {r.PartySize} • {r.Status}", r.CreatedAt));
            }

            foreach (var f in reviews)
            {
                feed.Add(new ActivityItem("fa-star", f.Rating >= 4 ? "icon-green" : f.Rating == 3 ? "icon-gold" : "icon-danger",
                    $"{f.Customer?.FullName} left a {f.Rating}-star review.",
                    "Customer feedback", f.CreatedAt));
            }

            return feed.OrderByDescending(a => a.When).Take(take).ToList();
        }

        // =========================================================
        // REPORTS
        // =========================================================
        public async Task<ReportResult> GetReportAsync(ReportType type, ReportRange range)
        {
            var (from, to, label) = GetPeriod(range);
            var prevFrom = from - (to - from);

            // Orders and bookings for this period AND the previous one (for the % change)
            var orders = await _db.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems).ThenInclude(i => i.MenuItem).ThenInclude(m => m!.MenuCategory)
                .Where(o => o.CreatedAt >= prevFrom && o.CreatedAt < to && o.Status != OrderStatus.Cancelled)
                .AsNoTracking()
                .ToListAsync();

            var bookings = await _db.Reservations
                .Include(r => r.Customer)
                .Include(r => r.RestaurantTable).ThenInclude(t => t!.DiningArea)
                .Where(r => r.StartTime >= prevFrom && r.StartTime < to)
                .AsNoTracking()
                .ToListAsync();

            var cur = orders.Where(o => o.CreatedAt >= from).ToList();
            var prev = orders.Where(o => o.CreatedAt < from).ToList();
            var curBookings = bookings.Where(b => b.StartTime >= from).ToList();
            var prevBookings = bookings.Where(b => b.StartTime < from).ToList();

            var report = new ReportResult
            {
                Type = type, Range = range, From = from, To = to, RangeLabel = label,
                Revenue = cur.Sum(o => o.Total),
                Orders = cur.Count,
                Reservations = curBookings.Count(b => b.Status != ReservationStatus.Cancelled),
                PrevRevenue = prev.Sum(o => o.Total),
                PrevOrders = prev.Count,
                PrevReservations = prevBookings.Count(b => b.Status != ReservationStatus.Cancelled)
            };
            report.AverageOrder = report.Orders == 0 ? 0 : Math.Round(report.Revenue / report.Orders, 2);
            report.PrevAverageOrder = report.PrevOrders == 0 ? 0 : Math.Round(report.PrevRevenue / report.PrevOrders, 2);

            var buckets = GetBuckets(range, from);

            switch (type)
            {
                case ReportType.Sales: BuildSales(report, cur, curBookings, buckets); break;
                case ReportType.Reservations: await BuildReservationsAsync(report, curBookings, buckets); break;
                case ReportType.Popular: BuildPopular(report, cur, buckets); break;
                case ReportType.Customer: await BuildCustomersAsync(report, cur, curBookings, buckets); break;
            }

            return report;
        }

        // ---- Sales Performance ----
        private static void BuildSales(ReportResult r, List<Order> orders, List<Reservation> bookings, List<Bucket> buckets)
        {
            r.TrendTitle = $"Sales Trend ({r.RangeLabel})";
            r.TrendLabel = "Revenue (RM)";
            r.TrendIsMoney = true;
            r.Trend = new ChartSeries(buckets.Select(b => b.Label).ToList(),
                                      buckets.Select(b => orders.Where(o => b.Contains(o.CreatedAt)).Sum(o => o.Total)).ToList());

            var byCategory = orders.SelectMany(o => o.OrderItems)
                .GroupBy(i => i.MenuItem?.MenuCategory?.Name ?? "Other")
                .Select(g => new { g.Key, Total = g.Sum(i => i.LineTotal) })
                .OrderByDescending(x => x.Total).ToList();
            r.BreakdownTitle = "Sales by Category";
            r.Breakdown = new ChartSeries(byCategory.Select(x => x.Key).ToList(), byCategory.Select(x => x.Total).ToList());

            r.Columns = new() { "Period", "Orders", "Revenue (RM)", "Reservations", "Top Item" };
            foreach (var b in buckets)
            {
                var inB = orders.Where(o => b.Contains(o.CreatedAt)).ToList();
                var top = inB.SelectMany(o => o.OrderItems).GroupBy(i => i.MenuItem?.Name ?? "-")
                             .OrderByDescending(g => g.Sum(i => i.Quantity)).Select(g => g.Key).FirstOrDefault() ?? "-";
                r.Rows.Add(new()
                {
                    b.LongLabel, inB.Count.ToString(), inB.Sum(o => o.Total).ToString("N2", Money),
                    bookings.Count(x => b.Contains(x.StartTime) && x.Status != ReservationStatus.Cancelled).ToString(), top
                });
            }
        }

        // ---- Reservation Statistics (+ peak hours and table utilisation) ----
        private async Task BuildReservationsAsync(ReportResult r, List<Reservation> bookings, List<Bucket> buckets)
        {
            var active = bookings.Where(b => b.Status != ReservationStatus.Cancelled).ToList();

            r.TrendTitle = $"Reservation Trend ({r.RangeLabel})";
            r.TrendLabel = "Bookings";
            r.Trend = new ChartSeries(buckets.Select(b => b.Label).ToList(),
                                      buckets.Select(b => (decimal)active.Count(x => b.Contains(x.StartTime))).ToList());

            // Peak dining hours
            var hours = active.GroupBy(b => b.StartTime.Hour).OrderBy(g => g.Key).ToList();
            r.BreakdownTitle = "Peak Dining Hours";
            r.BreakdownChart = "bar";
            r.Breakdown = new ChartSeries(hours.Select(g => DateTime.Today.AddHours(g.Key).ToString("h tt")).ToList(),
                                          hours.Select(g => (decimal)g.Count()).ToList());

            r.Columns = new() { "Period", "Bookings", "Guests", "Completed", "Cancelled / No-show", "Busiest Hour" };
            foreach (var b in buckets)
            {
                var inB = bookings.Where(x => b.Contains(x.StartTime)).ToList();
                var busiest = inB.Where(x => x.Status != ReservationStatus.Cancelled)
                                 .GroupBy(x => x.StartTime.Hour).OrderByDescending(g => g.Count())
                                 .Select(g => DateTime.Today.AddHours(g.Key).ToString("h tt")).FirstOrDefault() ?? "-";
                r.Rows.Add(new()
                {
                    b.LongLabel,
                    inB.Count(x => x.Status != ReservationStatus.Cancelled).ToString(),
                    inB.Where(x => x.Status != ReservationStatus.Cancelled).Sum(x => x.PartySize).ToString(),
                    inB.Count(x => x.Status == ReservationStatus.Completed).ToString(),
                    inB.Count(x => x.Status == ReservationStatus.Cancelled || x.Status == ReservationStatus.NoShow).ToString(),
                    busiest
                });
            }

            // Table utilisation = hours booked / hours the restaurant was open in this period
            var openHours = await GetOpenHoursAsync(r.From, r.To);
            var tables = await _db.RestaurantTables.Include(t => t.DiningArea).Where(t => t.IsActive).AsNoTracking().ToListAsync();
            var used = bookings.Where(b => b.Status != ReservationStatus.Cancelled && b.Status != ReservationStatus.NoShow).ToList();
            r.TableUsage = tables.Select(t =>
            {
                var mine = used.Where(b => b.RestaurantTableId == t.Id).ToList();
                var hoursBooked = mine.Sum(b => (b.EndTime - b.StartTime).TotalHours);
                var pct = openHours <= 0 ? 0 : Math.Min(100, Math.Round(hoursBooked / openHours * 100, 1));
                return new TableUsage(t.TableNumber, t.DiningArea?.Name ?? "-", t.Capacity, mine.Count, mine.Sum(b => b.PartySize),
                                      Math.Round(hoursBooked, 1), pct);
            }).OrderByDescending(t => t.UtilisationPercent).ToList();
        }

        // ---- Popular Menu Items ----
        private static void BuildPopular(ReportResult r, List<Order> orders, List<Bucket> buckets)
        {
            var lines = orders.SelectMany(o => o.OrderItems).ToList();
            var totalQty = lines.Sum(i => i.Quantity);
            var items = lines.GroupBy(i => i.MenuItemId)
                .Select(g => new
                {
                    Name = g.First().MenuItem?.Name ?? "Item",
                    Category = g.First().MenuItem?.MenuCategory?.Name ?? "-",
                    Qty = g.Sum(i => i.Quantity),
                    Revenue = g.Sum(i => i.LineTotal)
                })
                .OrderByDescending(x => x.Qty).ThenByDescending(x => x.Revenue).ToList();

            r.TrendTitle = $"Items Sold ({r.RangeLabel})";
            r.TrendLabel = "Items sold";
            r.Trend = new ChartSeries(buckets.Select(b => b.Label).ToList(),
                buckets.Select(b => (decimal)orders.Where(o => b.Contains(o.CreatedAt)).SelectMany(o => o.OrderItems).Sum(i => i.Quantity)).ToList());

            var top5 = items.Take(5).ToList();
            r.BreakdownTitle = "Top 5 Dishes";
            r.Breakdown = new ChartSeries(top5.Select(x => x.Name).ToList(), top5.Select(x => (decimal)x.Qty).ToList());

            r.Columns = new() { "Rank", "Menu Item", "Category", "Qty Sold", "Revenue (RM)", "Share of Items" };
            var rank = 1;
            foreach (var x in items)
            {
                r.Rows.Add(new()
                {
                    (rank++).ToString(), x.Name, x.Category, x.Qty.ToString(), x.Revenue.ToString("N2", Money),
                    totalQty == 0 ? "0%" : $"{Math.Round(x.Qty * 100.0 / totalQty, 1).ToString(Money)}%"
                });
            }
        }

        // ---- Customer Activity ----
        private async Task BuildCustomersAsync(ReportResult r, List<Order> orders, List<Reservation> bookings, List<Bucket> buckets)
        {
            var newCustomers = await _db.Customers.Where(c => c.CreatedAt >= r.From && c.CreatedAt < r.To)
                                                  .Select(c => c.CreatedAt).ToListAsync();

            r.TrendTitle = $"New Customer Sign-ups ({r.RangeLabel})";
            r.TrendLabel = "New customers";
            r.Trend = new ChartSeries(buckets.Select(b => b.Label).ToList(),
                                      buckets.Select(b => (decimal)newCustomers.Count(d => b.Contains(d))).ToList());

            r.BreakdownTitle = "Orders by Type";
            r.Breakdown = new ChartSeries(new() { "Dine-in", "Takeaway" }, new()
            {
                orders.Count(o => o.OrderType == OrderType.DineIn),
                orders.Count(o => o.OrderType == OrderType.Takeaway)
            });

            var ids = orders.Select(o => o.CustomerId).Concat(bookings.Select(b => b.CustomerId)).Distinct();
            var rows = ids.Select(id =>
            {
                var mine = orders.Where(o => o.CustomerId == id).ToList();
                var myBookings = bookings.Where(b => b.CustomerId == id && b.Status != ReservationStatus.Cancelled).ToList();
                var customer = mine.FirstOrDefault()?.Customer ?? myBookings.FirstOrDefault()?.Customer
                               ?? bookings.First(b => b.CustomerId == id).Customer;
                var last = mine.Select(o => o.CreatedAt).Concat(myBookings.Select(b => b.StartTime)).DefaultIfEmpty().Max();
                return new { customer, Orders = mine.Count, Spent = mine.Sum(o => o.Total), Bookings = myBookings.Count, Last = last };
            })
            .OrderByDescending(x => x.Spent).ThenByDescending(x => x.Bookings).ToList();

            r.Columns = new() { "Customer", "Email", "Orders", "Total Spent (RM)", "Bookings", "Last Activity" };
            foreach (var x in rows)
            {
                r.Rows.Add(new()
                {
                    x.customer?.FullName ?? "-", x.customer?.Email ?? "-", x.Orders.ToString(),
                    x.Spent.ToString("N2", Money), x.Bookings.ToString(),
                    x.Last == default ? "-" : x.Last.ToString("dd MMM yyyy, h:mm tt")
                });
            }
        }

        // =========================================================
        // CSV EXPORT (Advanced 4)
        // =========================================================
        public string ToCsv(ReportResult report)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Csv($"{report.Type} report", report.RangeLabel,
                              $"{report.From:yyyy-MM-dd} to {report.To.AddDays(-1):yyyy-MM-dd}"));
            sb.AppendLine(Csv("Total Revenue (RM)", report.Revenue.ToString("0.00", Money)));
            sb.AppendLine(Csv("Total Orders", report.Orders.ToString()));
            sb.AppendLine(Csv("Avg Order Value (RM)", report.AverageOrder.ToString("0.00", Money)));
            sb.AppendLine(Csv("Total Reservations", report.Reservations.ToString()));
            sb.AppendLine();
            sb.AppendLine(Csv(report.Columns.ToArray()));
            foreach (var row in report.Rows) sb.AppendLine(Csv(row.ToArray()));

            if (report.TableUsage.Any())
            {
                sb.AppendLine();
                sb.AppendLine(Csv("Table", "Area", "Seats", "Bookings", "Guests", "Hours Booked", "Utilisation %"));
                foreach (var t in report.TableUsage)
                    sb.AppendLine(Csv(t.Table, t.Area, t.Capacity.ToString(), t.Bookings.ToString(), t.Guests.ToString(),
                                      t.HoursBooked.ToString(Money), t.UtilisationPercent.ToString(Money)));
            }
            return sb.ToString();
        }

        // Quote every value so commas / quotes in names don't break the columns
        private static string Csv(params string[] values) =>
            string.Join(",", values.Select(v => "\"" + (v ?? string.Empty).Replace("\"", "\"\"") + "\""));

        // =========================================================
        // HELPERS
        // =========================================================
        public static (DateTime from, DateTime to, string label) GetPeriod(ReportRange range)
        {
            var today = DateTime.Today;
            return range switch
            {
                ReportRange.Today => (today, today.AddDays(1), "Today"),
                ReportRange.Month => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1), "This Month"),
                ReportRange.Year => (new DateTime(today.Year, 1, 1), new DateTime(today.Year + 1, 1, 1), "This Year"),
                _ => WeekOf(today)
            };
        }

        private static (DateTime, DateTime, string) WeekOf(DateTime day)
        {
            var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
            return (monday, monday.AddDays(7), "This Week");
        }

        // One bucket = one point on the chart / one row in the table
        private record Bucket(string Label, string LongLabel, DateTime Start, DateTime End)
        {
            public bool Contains(DateTime t) => t >= Start && t < End;
        }

        private static List<Bucket> GetBuckets(ReportRange range, DateTime from)
        {
            switch (range)
            {
                case ReportRange.Today:        // hourly, 8 AM - 11 PM
                    return Enumerable.Range(8, 16).Select(h => from.AddHours(h))
                        .Select(t => new Bucket(t.ToString("h tt"), t.ToString("h:mm tt") + " - " + t.AddHours(1).ToString("h:mm tt"), t, t.AddHours(1))).ToList();
                case ReportRange.Month:        // daily
                    var days = DateTime.DaysInMonth(from.Year, from.Month);
                    return Enumerable.Range(0, days).Select(i => from.AddDays(i))
                        .Select(d => new Bucket(d.ToString("d MMM"), d.ToString("MMM dd, yyyy (ddd)"), d, d.AddDays(1))).ToList();
                case ReportRange.Year:         // monthly
                    return Enumerable.Range(0, 12).Select(i => from.AddMonths(i))
                        .Select(m => new Bucket(m.ToString("MMM"), m.ToString("MMMM yyyy"), m, m.AddMonths(1))).ToList();
                default:                       // week: daily Mon-Sun
                    return Enumerable.Range(0, 7).Select(i => from.AddDays(i))
                        .Select(d => new Bucket(d.ToString("ddd"), d.ToString("MMM dd, yyyy (ddd)"), d, d.AddDays(1))).ToList();
            }
        }

        // Total opening hours between two dates, from the Opening Hours set by the admin
        private async Task<double> GetOpenHoursAsync(DateTime from, DateTime to)
        {
            var hours = await _db.OperatingHours.AsNoTracking().ToListAsync();
            double total = 0;
            for (var d = from; d < to; d = d.AddDays(1))
            {
                var h = hours.FirstOrDefault(x => x.Day == d.DayOfWeek);
                if (h == null || h.IsClosed) continue;
                var span = (h.CloseTime - h.OpenTime).TotalHours;
                if (span <= 0) span += 24;   // closes after midnight
                total += span;
            }
            return total;
        }
    }
}
