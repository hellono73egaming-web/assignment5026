using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Pages.StaffPanel
{
    // RS9-RS12, Intermediate #2: live kitchen board.
    // New (Pending) -> Preparing -> Ready to Serve -> Served (leaves the board, goes to billing).
    // Every move goes through OrderService, which enforces the allowed status changes
    // and notifies the customer.
    public class ActiveOrdersModel : PageModel
    {
        private readonly IOrderService _orders;

        public ActiveOrdersModel(IOrderService orders)
        {
            _orders = orders;
        }

        public List<Order> NewOrders { get; set; } = new();
        public List<Order> Preparing { get; set; } = new();
        public List<Order> Ready { get; set; } = new();

        [TempData] public string? Message { get; set; }
        [TempData] public bool MessageIsUndo { get; set; }
        [TempData] public string? Error { get; set; }

        public async Task OnGetAsync()
        {
            var active = await _orders.GetActiveOrdersAsync();   // oldest first
            NewOrders = active.Where(o => o.Status == OrderStatus.Pending).ToList();
            Preparing = active.Where(o => o.Status == OrderStatus.Preparing).ToList();
            Ready = active.Where(o => o.Status == OrderStatus.ReadyToServe).ToList();
        }

        public async Task<IActionResult> OnPostMoveAsync(int id, string orderNumber, OrderStatus status, bool undo = false)
        {
            var result = await _orders.UpdateStatusAsync(id, status);

            if (result.Success)
            {
                Message = status == OrderStatus.Served
                    ? $"Order #{orderNumber} served! Unpaid orders now wait in Billing; orders already paid online are completed."
                    : undo ? $"Undo successful. Order #{orderNumber} moved back."
                           : $"Order #{orderNumber} moved to {Services.OrderService.FriendlyStatus(status)}.";
                MessageIsUndo = undo;
            }
            else
            {
                // e.g. another staff member already moved it
                Error = result.Error;
            }

            return RedirectToPage();
        }

        // "Just now", "5 mins ago", "1 hr 10 mins ago"
        public static string TimeAgo(DateTime when)
        {
            var span = DateTime.Now - when;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min{((int)span.TotalMinutes == 1 ? "" : "s")} ago";
            return $"{(int)span.TotalHours} hr {span.Minutes} mins ago";
        }

        public static string TableLabel(Order o) =>
            o.Reservation?.RestaurantTable != null ? $"Table {o.Reservation.RestaurantTable.TableNumber}"
            : o.OrderType == OrderType.Takeaway ? "Takeaway"
            : "Dine-in";
    }
}
