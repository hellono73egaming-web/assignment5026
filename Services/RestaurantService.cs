using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // AD2 / Admin b - business rules for the restaurant layout:
    //  - table numbers are unique
    //  - an area with tables can't be deleted (deactivate the tables or move them first)
    //  - a table with upcoming bookings can't be deactivated (would strand customers)
    //  - closing time must be after opening time
    public class RestaurantService : IRestaurantService
    {
        private readonly ApplicationDbContext _db;

        public RestaurantService(ApplicationDbContext db)
        {
            _db = db;
        }

        private static readonly ReservationStatus[] ActiveStatuses =
        {
            ReservationStatus.Pending, ReservationStatus.Confirmed, ReservationStatus.Seated
        };

        // ------------------------------------------------------------
        // DINING AREAS
        // ------------------------------------------------------------
        public Task<List<DiningArea>> GetAreasAsync() =>
            _db.DiningAreas.Include(a => a.Tables).OrderBy(a => a.Name).ToListAsync();

        public async Task<ServiceResult> SaveAreaAsync(int id, string name, string? description, string? imageUrl)
        {
            name = name?.Trim() ?? string.Empty;
            if (name.Length < 2 || name.Length > 50)
                return ServiceResult.Fail("Area name must be 2-50 characters.");
            if (description?.Length > 250)
                return ServiceResult.Fail("Description can be at most 250 characters.");
            if (!string.IsNullOrWhiteSpace(imageUrl) && !imageUrl.Trim().StartsWith("/uploads/")
                && !Uri.IsWellFormedUriString(imageUrl.Trim(), UriKind.Absolute))
                return ServiceResult.Fail("Image must be an uploaded file or a full link starting with http:// or https://.");

            var duplicate = await _db.DiningAreas.AnyAsync(a => a.Id != id && a.Name.ToLower() == name.ToLower());
            if (duplicate) return ServiceResult.Fail($"An area called \"{name}\" already exists.");

            DiningArea? area;
            if (id == 0)
            {
                area = new DiningArea();
                _db.DiningAreas.Add(area);
            }
            else
            {
                area = await _db.DiningAreas.FindAsync(id);
                if (area == null) return ServiceResult.Fail("Dining area not found.");
            }

            area.Name = name;
            area.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            area.ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteAreaAsync(int id)
        {
            var area = await _db.DiningAreas.Include(a => a.Tables).FirstOrDefaultAsync(a => a.Id == id);
            if (area == null) return ServiceResult.Fail("Dining area not found.");

            if (area.Tables.Count > 0)
                return ServiceResult.Fail($"\"{area.Name}\" still has {area.Tables.Count} table(s). Move or remove them first.");

            _db.DiningAreas.Remove(area);
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // ------------------------------------------------------------
        // TABLES
        // ------------------------------------------------------------
        public Task<List<RestaurantTable>> GetTablesAsync() =>
            _db.RestaurantTables.Include(t => t.DiningArea)
               .OrderBy(t => t.DiningArea!.Name).ThenBy(t => t.TableNumber)
               .ToListAsync();

        public async Task<ServiceResult> SaveTableAsync(int id, string tableNumber, int capacity, int diningAreaId, bool isActive)
        {
            tableNumber = tableNumber?.Trim().ToUpper() ?? string.Empty;
            if (tableNumber.Length < 1 || tableNumber.Length > 10)
                return ServiceResult.Fail("Table number must be 1-10 characters, e.g. M1.");
            if (capacity < 1 || capacity > 20)
                return ServiceResult.Fail("Number of seats must be between 1 and 20.");
            if (!await _db.DiningAreas.AnyAsync(a => a.Id == diningAreaId))
                return ServiceResult.Fail("Please choose a dining area.");

            var duplicate = await _db.RestaurantTables.AnyAsync(t => t.Id != id && t.TableNumber == tableNumber);
            if (duplicate) return ServiceResult.Fail($"Table {tableNumber} already exists.");

            RestaurantTable? table;
            if (id == 0)
            {
                table = new RestaurantTable();
                _db.RestaurantTables.Add(table);
            }
            else
            {
                table = await _db.RestaurantTables.FindAsync(id);
                if (table == null) return ServiceResult.Fail("Table not found.");

                if (table.IsActive && !isActive)
                {
                    var blocked = await UpcomingBookingCountAsync(id);
                    if (blocked > 0)
                        return ServiceResult.Fail($"Table {table.TableNumber} has {blocked} upcoming booking(s). Cancel or move them before deactivating.");
                }

                // Shrinking a table must not break bookings that already need more seats
                var tooBig = await _db.Reservations.CountAsync(r => r.RestaurantTableId == id
                    && ActiveStatuses.Contains(r.Status) && r.StartTime >= DateTime.Now && r.PartySize > capacity);
                if (tooBig > 0)
                    return ServiceResult.Fail($"{tooBig} upcoming booking(s) on this table need more than {capacity} seats.");
            }

            table.TableNumber = tableNumber;
            table.Capacity = capacity;
            table.DiningAreaId = diningAreaId;
            table.IsActive = isActive;

            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> SetTableActiveAsync(int id, bool isActive)
        {
            var table = await _db.RestaurantTables.FindAsync(id);
            if (table == null) return ServiceResult.Fail("Table not found.");

            if (!isActive)
            {
                var blocked = await UpcomingBookingCountAsync(id);
                if (blocked > 0)
                    return ServiceResult.Fail($"Table {table.TableNumber} has {blocked} upcoming booking(s). Cancel or move them before deactivating.");
            }

            table.IsActive = isActive;
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        private Task<int> UpcomingBookingCountAsync(int tableId) =>
            _db.Reservations.CountAsync(r => r.RestaurantTableId == tableId
                && ActiveStatuses.Contains(r.Status) && r.StartTime >= DateTime.Now);

        // ------------------------------------------------------------
        // OPENING HOURS
        // ------------------------------------------------------------
        public async Task<List<OperatingHour>> GetOperatingHoursAsync()
        {
            var hours = await _db.OperatingHours.ToListAsync();

            // Make sure all 7 days exist (default 11:00 - 22:00)
            foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
            {
                if (!hours.Any(h => h.Day == day))
                    hours.Add(new OperatingHour { Day = day, OpenTime = new TimeSpan(11, 0, 0), CloseTime = new TimeSpan(22, 0, 0) });
            }

            // Monday first, Sunday last
            return hours.OrderBy(h => ((int)h.Day + 6) % 7).ToList();
        }

        public async Task<ServiceResult> SaveOperatingHoursAsync(IEnumerable<OperatingHour> hours)
        {
            var list = hours.ToList();
            if (list.Count == 0) return ServiceResult.Fail("No opening hours were submitted.");

            foreach (var h in list.Where(h => !h.IsClosed))
            {
                if (h.CloseTime <= h.OpenTime)
                    return ServiceResult.Fail($"{h.Day}: closing time must be after opening time.");
            }

            var existing = await _db.OperatingHours.ToListAsync();
            foreach (var h in list)
            {
                var row = existing.FirstOrDefault(e => e.Day == h.Day);
                if (row == null)
                {
                    row = new OperatingHour { Day = h.Day };
                    _db.OperatingHours.Add(row);
                }
                row.OpenTime = h.OpenTime;
                row.CloseTime = h.CloseTime;
                row.IsClosed = h.IsClosed;
            }

            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }
    }
}
