using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    public record ChatRequest(string Message);

    // What the chat widget shows: the answer, an optional button, and quick-reply chips
    public record ChatReply(string Text, List<string> Suggestions, string? Link = null, string? LinkText = null);

    // Rule-based assistant: works out the customer's INTENT from keywords, then answers
    // with LIVE data from the database (opening hours, their booking, their order, menu prices,
    // current promotions, recommendations). No external AI service or API key is needed.
    public class ChatbotService : IChatbotService
    {
        private readonly ApplicationDbContext _db;
        private readonly IRecommendationService _recommendations;
        private readonly IPromotionService _promotions;

        private static readonly List<string> DefaultChips = new()
        {
            "Opening hours", "Book a table", "Recommend a dish", "My order status", "Any promotions?"
        };

        public ChatbotService(ApplicationDbContext db, IRecommendationService recommendations, IPromotionService promotions)
        {
            _db = db;
            _recommendations = recommendations;
            _promotions = promotions;
        }

        public async Task<ChatReply> ReplyAsync(string message, string? customerId)
        {
            var text = (message ?? string.Empty).Trim();
            if (text.Length == 0) return Help();
            if (text.Length > 500) text = text[..500];
            var m = text.ToLowerInvariant();

            // Order matters: the more specific intents are checked first
            if (Has(m, "hi", "hello", "hey", "good morning", "good evening") && m.Length < 25) return Greeting();
            if (Has(m, "order", "orders", "food") && Has(m, "status", "where", "track", "tracking", "ready", "my order")) return await OrderStatusAsync(customerId);
            if (Has(m, "my reservation", "my reservations", "my booking", "my bookings", "booking status", "reservation status")) return await MyReservationAsync(customerId);
            if (Has(m, "cancel")) return CancelHelp();
            if (Has(m, "book", "booking", "bookings", "reserve", "reservation", "reservations", "table", "tables")) return await BookingHelpAsync(customerId);
            if (Has(m, "hour", "hours", "open", "opened", "close", "closed", "closing", "opening", "what time")) return await OpeningHoursAsync();
            if (Has(m, "promo", "promos", "promotion", "promotions", "discount", "discounts", "deal", "deals", "offer", "offers", "sale")) return await PromotionsAsync();
            if (Has(m, "recommend", "recommendation", "recommendations", "suggest", "suggestion", "popular", "best", "special", "specials", "what should")) return await RecommendAsync(customerId);
            if (Has(m, "vegetarian", "vegan", "spicy", "gluten", "halal", "seafood", "dessert", "drink", "beverage"))
                return await DietaryAsync(m);
            if (Has(m, "price", "prices", "how much", "cost", "costs")) return await PriceAsync(m) ?? await MenuOverviewAsync();
            if (Has(m, "pay", "payment", "card", "cash", "e-wallet", "ewallet")) return PaymentHelp();
            if (Has(m, "where", "address", "location", "contact", "phone", "email")) return await ContactAsync();
            if (Has(m, "menu", "food", "dish", "eat")) return await MenuOverviewAsync();

            // Last try: did they type a dish name?
            return await PriceAsync(m) ?? Fallback();
        }

        // ---------------- intents ----------------
        private static ChatReply Greeting() =>
            new("Ciao! I'm Artie's assistant. I can help with bookings, the menu, your order and our opening hours.", DefaultChips);

        private static ChatReply Help() =>
            new("Ask me about opening hours, booking a table, dish prices, promotions or your order status.", DefaultChips);

        private static ChatReply Fallback() =>
            new("Sorry, I didn't quite get that. Try one of these, or ask about a dish by name.", DefaultChips);

        private async Task<ChatReply> OpeningHoursAsync()
        {
            var hours = await _db.OperatingHours.AsNoTracking().ToListAsync();
            if (hours.Count == 0) return new("Our opening hours haven't been set yet. Please call the restaurant.", DefaultChips);

            // Monday first
            var lines = hours.OrderBy(h => ((int)h.Day + 6) % 7).Select(h =>
                $"{h.Day}: " + (h.IsClosed ? "Closed" : $"{Fmt(h.OpenTime)} - {Fmt(h.CloseTime)}"));

            var today = hours.FirstOrDefault(h => h.Day == DateTime.Today.DayOfWeek);
            var now = DateTime.Now.TimeOfDay;
            var openNow = today != null && !today.IsClosed && now >= today.OpenTime && now < today.CloseTime;

            return new($"We're {(openNow ? "open now" : "closed right now")}.\n" + string.Join("\n", lines),
                       new() { "Book a table", "Recommend a dish" }, "/CustomerPanel/BookTable", "Book a table");
        }

        private async Task<ChatReply> BookingHelpAsync(string? customerId)
        {
            var settings = await _db.RestaurantSettings.AsNoTracking().FirstOrDefaultAsync();
            var tables = await _db.RestaurantTables.Where(t => t.IsActive).Select(t => t.Capacity).ToListAsync();
            var biggest = tables.DefaultIfEmpty(0).Max();
            var minutes = settings?.ReservationDurationMinutes ?? 120;

            var text = "You can book online: pick a date, time and party size and we'll find a free table for you. "
                     + $"Each booking holds the table for {minutes / 60.0:0.#} hours"
                     + (biggest > 0 ? $", and our largest table seats {biggest}." : ".");

            if (customerId != null)
            {
                var next = await NextReservationAsync(customerId);
                if (next != null) text += $"\nYou already have a booking on {next.StartTime:ddd d MMM, h:mm tt} ({next.Status}).";
            }
            return new(text, new() { "My reservation", "Opening hours" }, "/CustomerPanel/BookTable", "Book a table");
        }

        private async Task<ChatReply> MyReservationAsync(string? customerId)
        {
            if (customerId == null) return LoginNeeded();
            var next = await NextReservationAsync(customerId);
            if (next == null)
                return new("You don't have any upcoming reservations.", new() { "Book a table" }, "/CustomerPanel/BookTable", "Book a table");

            return new($"Your next booking {next.ReservationCode} is on {next.StartTime:dddd d MMM 'at' h:mm tt} "
                     + $"for {next.PartySize} at table {next.RestaurantTable?.TableNumber}. Status: {next.Status}.",
                       new() { "Cancel a booking", "Opening hours" }, "/CustomerPanel/MyReservations", "My reservations");
        }

        private async Task<ChatReply> OrderStatusAsync(string? customerId)
        {
            if (customerId == null) return LoginNeeded();
            var order = await _db.Orders
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (order == null)
                return new("You haven't placed any orders yet.", new() { "Recommend a dish" }, "/CustomerPanel/Menu", "Open the menu");

            var status = OrderService.FriendlyStatus(order.Status);
            var extra = order.Status switch
            {
                OrderStatus.Pending => " The kitchen will start on it shortly.",
                OrderStatus.Preparing => " Our chefs are cooking it now.",
                OrderStatus.ReadyToServe => " It's ready and on its way to you!",
                _ => ""
            };
            return new($"Your latest order #{order.OrderNumber} (RM {order.Total.ToString("N2", CultureInfo.InvariantCulture)}) is: {status}.{extra}",
                       new() { "My reservation", "Any promotions?" }, "/CustomerPanel/MyOrders", "Track my order");
        }

        private static ChatReply CancelHelp() =>
            new("To cancel a booking, open My Reservations and press Cancel on it. "
              + "An order can be cancelled from My Orders while it is still Pending (before the kitchen starts).",
                new() { "My reservation", "My order status" }, "/CustomerPanel/MyReservations", "My reservations");

        private async Task<ChatReply> PromotionsAsync()
        {
            var promos = await _promotions.GetActiveAsync();
            if (promos.Count == 0)
                return new("There are no promotions running right now. Check back soon!", new() { "Recommend a dish" });

            var lines = promos.Take(5).Select(p =>
                $"• {p.Title}: {p.DiscountPercent:0.##}% off {(p.MenuItem != null ? p.MenuItem.Name : "your order")} until {p.EndDate:d MMM}");
            return new("Current promotions (applied automatically at checkout):\n" + string.Join("\n", lines),
                       new() { "Recommend a dish" }, "/CustomerPanel/Menu", "Order now");
        }

        private async Task<ChatReply> RecommendAsync(string? customerId)
        {
            var recs = customerId != null
                ? await _recommendations.GetForCustomerAsync(customerId, 3)
                : await _recommendations.GetPopularAsync(3);
            if (recs.Count == 0) return await MenuOverviewAsync();

            var lines = recs.Select(r => $"• {r.Item.Name} (RM {r.Item.Price.ToString("N2", CultureInfo.InvariantCulture)}) - {r.Reason}");
            return new("You might enjoy:\n" + string.Join("\n", lines), new() { "Any promotions?", "Book a table" },
                       "/CustomerPanel/Menu", "Open the menu");
        }

        private async Task<ChatReply> DietaryAsync(string m)
        {
            var words = new[] { "vegetarian", "vegan", "spicy", "gluten", "halal", "seafood", "dessert", "drink", "beverage" }
                .Where(w => m.Contains(w)).ToList();

            var items = await _db.MenuItems.Include(i => i.MenuCategory).Where(i => i.IsAvailable).AsNoTracking().ToListAsync();
            var matches = items.Where(i => words.Any(w =>
                    (i.Tags ?? "").Contains(w, StringComparison.OrdinalIgnoreCase)
                 || (i.Description ?? "").Contains(w, StringComparison.OrdinalIgnoreCase)
                 || (i.MenuCategory?.Name ?? "").Contains(w, StringComparison.OrdinalIgnoreCase)
                 || ((w == "drink" || w == "beverage") && i.ItemType == MenuItemType.Beverage)))
                .Take(6).ToList();

            if (matches.Count == 0)
                return new($"I couldn't find dishes marked {string.Join("/", words)}. Our staff can help with special requests when you book.",
                           DefaultChips, "/CustomerPanel/Menu", "Open the menu");

            return new($"Here's what I found for {string.Join("/", words)}:\n"
                       + string.Join("\n", matches.Select(i => $"• {i.Name} - RM {i.Price.ToString("N2", CultureInfo.InvariantCulture)}")),
                       new() { "Recommend a dish" }, "/CustomerPanel/Menu", "Open the menu");
        }

        // Finds a dish by name in the message and gives its price and availability
        private async Task<ChatReply?> PriceAsync(string m)
        {
            var items = await _db.MenuItems.AsNoTracking().ToListAsync();
            var words = Regex.Split(m, @"[^a-z]+").Where(w => w.Length >= 4).ToHashSet();

            var best = items
                .Select(i => new { Item = i, Score = Regex.Split(i.Name.ToLowerInvariant(), @"[^a-z]+").Count(w => w.Length >= 4 && words.Contains(w)) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Select(x => x.Item)
                .FirstOrDefault();

            if (best == null) return null;
            var avail = best.IsAvailable ? "available today" : "sold out right now";
            return new($"{best.Name} is RM {best.Price.ToString("N2", CultureInfo.InvariantCulture)} and is {avail}."
                       + (string.IsNullOrEmpty(best.Description) ? "" : $"\n{best.Description}"),
                       new() { "Recommend a dish", "Any promotions?" }, "/CustomerPanel/Menu", "Order it");
        }

        private async Task<ChatReply> MenuOverviewAsync()
        {
            var categories = await _db.MenuCategories.OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();
            var text = categories.Count == 0
                ? "Our menu is being updated."
                : "Our menu has: " + string.Join(", ", categories) + ". Ask me about any dish by name for its price.";
            return new(text, new() { "Recommend a dish", "Vegetarian options" }, "/CustomerPanel/Menu", "Open the menu");
        }

        private static ChatReply PaymentHelp() =>
            new("You pay at the counter after your meal, with cash or e-wallet. Our staff will confirm the payment "
              + "and your receipt will show as PAID in My Orders.",
                new() { "My order status" }, "/CustomerPanel/MyOrders", "My orders");

        private async Task<ChatReply> ContactAsync()
        {
            var s = await _db.RestaurantSettings.AsNoTracking().FirstOrDefaultAsync();
            if (s == null) return new("Contact details haven't been set yet.", DefaultChips);
            var parts = new List<string> { s.RestaurantName };
            if (!string.IsNullOrEmpty(s.Address)) parts.Add(s.Address);
            if (!string.IsNullOrEmpty(s.Phone)) parts.Add("Tel: " + s.Phone);
            if (!string.IsNullOrEmpty(s.Email)) parts.Add("Email: " + s.Email);
            return new(string.Join("\n", parts), new() { "Opening hours", "Book a table" });
        }

        private static ChatReply LoginNeeded() =>
            new("Please log in so I can look that up for you.", new() { "Opening hours", "Recommend a dish" }, "/Identity/Account/Login", "Log in");

        // ---------------- helpers ----------------
        private Task<Reservation?> NextReservationAsync(string customerId) =>
            _db.Reservations.Include(r => r.RestaurantTable)
               .Where(r => r.CustomerId == customerId && r.StartTime >= DateTime.Now
                           && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
               .OrderBy(r => r.StartTime)
               .AsNoTracking()
               .FirstOrDefaultAsync();

        // Whole-word / phrase match so "hi" doesn't match "chicken"
        private static bool Has(string text, params string[] keys) =>
            keys.Any(k => Regex.IsMatch(text, $@"(^|[^a-z]){Regex.Escape(k)}([^a-z]|$)"));

        private static string Fmt(TimeSpan t) => DateTime.Today.Add(t).ToString("h:mm tt");
    }
}
