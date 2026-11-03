using WebApplication2.Services.Interfaces;
using System.Text.Json;

namespace WebApplication2.Services
{
    // One line in the shopping cart (stored in the user's session, not the database,
    // so an abandoned cart never becomes a fake order).
    public record CartLine(int MenuItemId, int Quantity, string? Remarks);

    // CS10 / CS11 - shopping cart kept in the session until the customer places the order.
    // Approach: cart kept in ASP.NET Core session state as JSON, following Microsoft Learn
    // "Session and state management in ASP.NET Core" (non-critical, temporary data only).
    public class CartService : ICartService
    {
        private const string SessionKey = "RROMS_Cart";
        public const int MaxQuantityPerItem = 50;

        private readonly IHttpContextAccessor _http;

        public CartService(IHttpContextAccessor http)
        {
            _http = http;
        }

        private ISession Session =>
            _http.HttpContext?.Session ?? throw new InvalidOperationException("Session is not available.");

        public List<CartLine> GetLines()
        {
            var json = Session.GetString(SessionKey);
            return string.IsNullOrEmpty(json)
                ? new List<CartLine>()
                : JsonSerializer.Deserialize<List<CartLine>>(json) ?? new List<CartLine>();
        }

        private void Save(List<CartLine> lines) =>
            Session.SetString(SessionKey, JsonSerializer.Serialize(lines));

        public void Add(int menuItemId, int quantity = 1, string? remarks = null)
        {
            if (quantity < 1) return;

            var lines = GetLines();
            var existing = lines.FirstOrDefault(l => l.MenuItemId == menuItemId);

            if (existing != null)
            {
                lines.Remove(existing);
                var newQty = Math.Min(existing.Quantity + quantity, MaxQuantityPerItem);
                lines.Add(existing with { Quantity = newQty, Remarks = remarks ?? existing.Remarks });
            }
            else
            {
                lines.Add(new CartLine(menuItemId, Math.Min(quantity, MaxQuantityPerItem), remarks));
            }

            Save(lines);
        }

        // Quantity 0 removes the item
        public void UpdateQuantity(int menuItemId, int quantity)
        {
            var lines = GetLines();
            var existing = lines.FirstOrDefault(l => l.MenuItemId == menuItemId);
            if (existing == null) return;

            lines.Remove(existing);
            if (quantity > 0)
                lines.Add(existing with { Quantity = Math.Min(quantity, MaxQuantityPerItem) });

            Save(lines);
        }

        public void Remove(int menuItemId) => UpdateQuantity(menuItemId, 0);

        public void Clear() => Session.Remove(SessionKey);

        public int ItemCount() => GetLines().Sum(l => l.Quantity);
    }
}
