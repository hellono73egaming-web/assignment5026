using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // One suggested dish and why it was suggested (shown to the customer)
    public record Recommendation(MenuItem Item, string Reason);

    // Business tier for recommendations (Advanced 7). Simple, explainable rules instead of a black box:
    //   1. "Order again"    - the customer's own most-ordered dishes
    //   2. "Because you like <category>" - popular dishes they haven't tried in their favourite category
    //   3. "Popular this week" - best sellers of the last 7 days (fallback: last 30 days)
    // Only available items are ever suggested, and each dish appears once.
    public class RecommendationService : IRecommendationService
    {
        private readonly ApplicationDbContext _db;

        public RecommendationService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<Recommendation>> GetPopularAsync(int take = 4)
        {
            var week = await PopularSinceAsync(DateTime.Today.AddDays(-7));
            var reason = "Popular this week";
            if (week.Count < take)
            {
                week = await PopularSinceAsync(DateTime.Today.AddDays(-30));
                reason = "Popular this month";
            }

            var items = await LoadAvailableAsync(week.Select(p => p.MenuItemId));
            var result = week.Where(p => items.ContainsKey(p.MenuItemId))
                             .Select(p => new Recommendation(items[p.MenuItemId], reason))
                             .Take(take)
                             .ToList();

            // New restaurant with no sales yet: fall back to the newest available dishes
            if (result.Count < take)
            {
                var used = result.Select(r => r.Item.Id).ToHashSet();
                var extra = await _db.MenuItems.Include(m => m.MenuCategory)
                    .Where(m => m.IsAvailable && !used.Contains(m.Id))
                    .OrderByDescending(m => m.Id)
                    .Take(take - result.Count)
                    .AsNoTracking()
                    .ToListAsync();
                result.AddRange(extra.Select(m => new Recommendation(m, "Chef's pick")));
            }
            return result;
        }

        public async Task<List<Recommendation>> GetForCustomerAsync(string customerId, int take = 4)
        {
            // Everything this customer has ordered (not cancelled)
            var history = await _db.OrderItems
                .Where(i => i.Order!.CustomerId == customerId && i.Order.Status != OrderStatus.Cancelled)
                .GroupBy(i => i.MenuItemId)
                .Select(g => new { MenuItemId = g.Key, Qty = g.Sum(i => i.Quantity) })
                .OrderByDescending(x => x.Qty)
                .ToListAsync();

            if (history.Count == 0) return await GetPopularAsync(take);   // new customer

            var result = new List<Recommendation>();
            var used = new HashSet<int>();

            // 1. Order again (top 2 of their favourites)
            var favourites = await LoadAvailableAsync(history.Select(h => h.MenuItemId));
            foreach (var h in history.Where(h => favourites.ContainsKey(h.MenuItemId)).Take(2))
            {
                result.Add(new Recommendation(favourites[h.MenuItemId], "Order again"));
                used.Add(h.MenuItemId);
            }

            // 2. Popular dishes in their favourite category that they haven't tried
            var tried = history.Select(h => h.MenuItemId).ToHashSet();
            var favCategory = favourites.Values
                .GroupBy(m => m.MenuCategoryId)
                .OrderByDescending(g => g.Sum(m => history.First(h => h.MenuItemId == m.Id).Qty))
                .Select(g => g.First().MenuCategory)
                .FirstOrDefault();

            if (favCategory != null)
            {
                var popular = await PopularSinceAsync(DateTime.Today.AddDays(-30));
                var candidates = await _db.MenuItems.Include(m => m.MenuCategory)
                    .Where(m => m.IsAvailable && m.MenuCategoryId == favCategory.Id && !tried.Contains(m.Id))
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var m in candidates
                             .OrderByDescending(m => popular.FirstOrDefault(p => p.MenuItemId == m.Id)?.Qty ?? 0)
                             .Take(take - result.Count))
                {
                    result.Add(new Recommendation(m, $"Because you like {favCategory.Name}"));
                    used.Add(m.Id);
                }
            }

            // 3. Fill the rest with best sellers
            if (result.Count < take)
            {
                foreach (var p in await GetPopularAsync(take * 2))
                {
                    if (result.Count >= take) break;
                    if (used.Add(p.Item.Id)) result.Add(p);
                }
            }
            return result;
        }

        // ---- helpers ----
        private record Popularity(int MenuItemId, int Qty);

        private async Task<List<Popularity>> PopularSinceAsync(DateTime since)
        {
            // Sort in SQL on an anonymous type (EF can translate that), then map to the record
            var rows = await _db.OrderItems
               .Where(i => i.Order!.CreatedAt >= since && i.Order.Status != OrderStatus.Cancelled)
               .GroupBy(i => i.MenuItemId)
               .Select(g => new { MenuItemId = g.Key, Qty = g.Sum(i => i.Quantity) })
               .OrderByDescending(x => x.Qty)
               .ToListAsync();
            return rows.Select(r => new Popularity(r.MenuItemId, r.Qty)).ToList();
        }

        private async Task<Dictionary<int, MenuItem>> LoadAvailableAsync(IEnumerable<int> ids)
        {
            var list = ids.ToList();
            return await _db.MenuItems.Include(m => m.MenuCategory)
                .Where(m => list.Contains(m.Id) && m.IsAvailable)
                .AsNoTracking()
                .ToDictionaryAsync(m => m.Id);
        }
    }
}
