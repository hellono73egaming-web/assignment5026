using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // Data shapes returned to the dashboard pages (simple DTOs, no database access)
    public record AdminDashboardStats(int CustomerCount, int StaffCount, int TodayReservations,
                                      int ActiveOrders, decimal TodaySales);

    public record StaffDashboardStats(int TodayReservations, int PendingReservations,
                                      int PendingOrders, int PreparingOrders);

    public record CustomerDashboardData(Customer? Customer, List<Reservation> UpcomingReservations,
                                        List<Notification> Notifications, Order? ActiveOrder, int UnreadCount);

    public record StaffDashboardData(StaffDashboardStats Stats, List<Reservation> NextReservations,
                                     List<Order> OrdersNeedingAction, decimal UnpaidTotal, int UnpaidBills);

    // Business tier for the Admin / Staff / Customer dashboards (3-tier architecture:
    // Razor Page -> DashboardService -> ApplicationDbContext -> SQL Server).
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public DashboardService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public async Task<AdminDashboardStats> GetAdminStatsAsync()
        {
            var (today, tomorrow) = TodayRange();

            var customers = await _db.Customers.CountAsync();
            var staff = await _db.StaffMembers.CountAsync();
            var reservations = await _db.Reservations
                .CountAsync(r => r.StartTime >= today && r.StartTime < tomorrow);
            var activeOrders = await _db.Orders
                .CountAsync(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
            var sales = await _db.Payments
                .Where(p => p.Status == PaymentStatus.Paid && p.PaidAt >= today && p.PaidAt < tomorrow)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;

            return new AdminDashboardStats(customers, staff, reservations, activeOrders, sales);
        }

        public async Task<StaffDashboardStats> GetStaffStatsAsync()
        {
            var (today, tomorrow) = TodayRange();

            var todayReservations = await _db.Reservations
                .CountAsync(r => r.StartTime >= today && r.StartTime < tomorrow);
            var pendingReservations = await _db.Reservations
                .CountAsync(r => r.Status == ReservationStatus.Pending);
            var pendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
            var preparingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Preparing);

            return new StaffDashboardStats(todayReservations, pendingReservations, pendingOrders, preparingOrders);
        }

        // Staff dashboard: counts + what needs doing next
        public async Task<StaffDashboardData> GetStaffDashboardAsync()
        {
            var stats = await GetStaffStatsAsync();
            var (today, tomorrow) = TodayRange();

            // Today's bookings that still need action (not finished / cancelled), soonest first
            var next = await _db.Reservations
                .Include(r => r.Customer)
                .Include(r => r.RestaurantTable)
                .Where(r => r.StartTime >= today && r.StartTime < tomorrow
                            && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed
                                || r.Status == ReservationStatus.Seated))
                .OrderBy(r => r.StartTime)
                .Take(6)
                .AsNoTracking()
                .ToListAsync();

            // Orders the floor/kitchen must act on: new ones and ones ready to serve
            var orders = await _db.Orders
                .Include(o => o.Customer)
                .Include(o => o.OrderItems).ThenInclude(i => i.MenuItem)
                .Include(o => o.Reservation).ThenInclude(r => r!.RestaurantTable)
                .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.ReadyToServe)
                .OrderBy(o => o.CreatedAt)
                .Take(6)
                .AsNoTracking()
                .ToListAsync();

            // Bills waiting to be settled
            var unpaid = await _db.Orders
                .Where(o => o.Status != OrderStatus.Cancelled
                            && (o.Payment == null || o.Payment.Status != PaymentStatus.Paid))
                .Select(o => o.Total)
                .ToListAsync();

            return new StaffDashboardData(stats, next, orders, unpaid.Sum(), unpaid.Count);
        }

        public async Task<CustomerDashboardData> GetCustomerDashboardAsync(string customerId)
        {
            var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);

            var upcoming = await _db.Reservations
                .Include(r => r.RestaurantTable)
                .Where(r => r.CustomerId == customerId && r.StartTime >= DateTime.Now
                            && r.Status != ReservationStatus.Cancelled)
                .OrderBy(r => r.StartTime)
                .Take(5)
                .ToListAsync();

            var notifications = await _notifications.GetLatestAsync(customerId, 5);
            var unread = await _notifications.GetUnreadCountAsync(customerId);

            // Most recent order that is still in progress (for the live tracker)
            var activeOrder = await _db.Orders
                .Include(o => o.OrderItems).ThenInclude(i => i.MenuItem)
                .Include(o => o.Reservation).ThenInclude(r => r!.RestaurantTable)
                .Where(o => o.CustomerId == customerId
                            && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            return new CustomerDashboardData(customer, upcoming, notifications, activeOrder, unread);
        }

        private static (DateTime today, DateTime tomorrow) TodayRange() =>
            (DateTime.Today, DateTime.Today.AddDays(1));
    }
}
