using Microsoft.EntityFrameworkCore;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // CS7-CS9, RS4-RS8, Intermediate #1 (automated availability checking + validation)
    // Approaches used (adapted, not copied):
    //  - Interval overlap test "startA < endB && endA > startB" (half-open ranges),
    //    the standard booking-system check (Stack Overflow Q325933).
    //  - Allowed status changes kept in a transition table, similar in idea to the
    //    "Permit" configuration of the Stateless state-machine library.
    //  - Result objects instead of exceptions for business-rule failures (Result pattern, Ardalis.Result).
    public class ReservationService : IReservationService
    {
        private readonly ApplicationDbContext _db;
        private readonly INotificationService _notifications;

        public ReservationService(ApplicationDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        // Reservations in these statuses occupy a table
        private static readonly ReservationStatus[] ActiveStatuses =
        {
            ReservationStatus.Pending, ReservationStatus.Confirmed, ReservationStatus.Seated
        };

        // Which status changes are allowed (state machine)
        private static readonly Dictionary<ReservationStatus, ReservationStatus[]> AllowedTransitions = new()
        {
            [ReservationStatus.Pending]   = new[] { ReservationStatus.Confirmed, ReservationStatus.Cancelled },
            [ReservationStatus.Confirmed] = new[] { ReservationStatus.Seated, ReservationStatus.Cancelled, ReservationStatus.NoShow },
            [ReservationStatus.Seated]    = new[] { ReservationStatus.Completed },
            [ReservationStatus.Completed] = Array.Empty<ReservationStatus>(),
            [ReservationStatus.Cancelled] = Array.Empty<ReservationStatus>(),
            [ReservationStatus.NoShow]    = Array.Empty<ReservationStatus>()
        };

        public const int MinPartySize = 1;
        public const int MaxPartySize = 20;
        public const int MinMinutesInAdvance = 30;
        public const int MaxDaysInAdvance = 60;

        public static IReadOnlyList<ReservationStatus> GetNextStatuses(ReservationStatus current) =>
            AllowedTransitions[current];

        public async Task<RestaurantSetting> GetSettingsAsync() =>
            await _db.RestaurantSettings.AsNoTracking().FirstOrDefaultAsync()
            ?? new RestaurantSetting { RestaurantName = "RROMS", ReservationDurationMinutes = 120 };

        // ------------------------------------------------------------
        // VALIDATION
        // ------------------------------------------------------------

        // Returns an error message, or null if the time slot is acceptable.
        public async Task<string?> ValidateTimeSlotAsync(DateTime start, DateTime end)
        {
            if (start < DateTime.Now.AddMinutes(MinMinutesInAdvance))
                return $"Reservations must be made at least {MinMinutesInAdvance} minutes in advance.";

            if (start > DateTime.Today.AddDays(MaxDaysInAdvance))
                return $"Reservations can only be made up to {MaxDaysInAdvance} days ahead.";

            var hours = await _db.OperatingHours.AsNoTracking()
                .FirstOrDefaultAsync(h => h.Day == start.DayOfWeek);

            if (hours == null || hours.IsClosed)
                return $"The restaurant is closed on {start.DayOfWeek}s.";

            var openAt = start.Date + hours.OpenTime;
            var closeAt = start.Date + hours.CloseTime;

            if (start < openAt || end > closeAt)
                return $"On {start.DayOfWeek}s we are open {FormatTime(hours.OpenTime)} - {FormatTime(hours.CloseTime)}. " +
                       $"Your booking ({start:h:mm tt} - {end:h:mm tt}) must fit inside these hours.";

            return null;
        }

        private static string ValidatePartySize(int partySize) =>
            partySize < MinPartySize || partySize > MaxPartySize
                ? $"Party size must be between {MinPartySize} and {MaxPartySize}."
                : string.Empty;

        // Two bookings overlap if: newStart < existingEnd AND newEnd > existingStart
        public Task<bool> IsTableFreeAsync(int tableId, DateTime start, DateTime end, int? ignoreReservationId = null) =>
            _db.Reservations.AllAsync(r =>
                r.RestaurantTableId != tableId
                || r.Id == ignoreReservationId
                || !ActiveStatuses.Contains(r.Status)
                || !(start < r.EndTime && end > r.StartTime));

        // All active tables big enough and free for the whole slot, smallest first
        public async Task<List<RestaurantTable>> GetAvailableTablesAsync(DateTime start, int partySize,
                                                                         int? ignoreReservationId = null)
        {
            var settings = await GetSettingsAsync();
            var end = start.AddMinutes(settings.ReservationDurationMinutes);

            var busyTableIds = await _db.Reservations
                .Where(r => r.Id != ignoreReservationId
                            && ActiveStatuses.Contains(r.Status)
                            && start < r.EndTime && end > r.StartTime)
                .Select(r => r.RestaurantTableId)
                .Distinct()
                .ToListAsync();

            return await _db.RestaurantTables
                .Include(t => t.DiningArea)
                .Where(t => t.IsActive && t.Capacity >= partySize && !busyTableIds.Contains(t.Id))
                .OrderBy(t => t.Capacity)
                .ThenBy(t => t.TableNumber)
                .ToListAsync();
        }

        // ------------------------------------------------------------
        // CUSTOMER ACTIONS
        // ------------------------------------------------------------

        // CS7 - make a reservation. If no table is chosen, the smallest suitable free table is assigned.
        public async Task<ServiceResult<Reservation>> CreateAsync(string customerId, DateTime start, int partySize,
                                                                  string? specialRequests, int? preferredTableId = null)
        {
            var sizeError = ValidatePartySize(partySize);
            if (sizeError != string.Empty) return ServiceResult<Reservation>.Fail(sizeError);

            var settings = await GetSettingsAsync();
            var end = start.AddMinutes(settings.ReservationDurationMinutes);

            var timeError = await ValidateTimeSlotAsync(start, end);
            if (timeError != null) return ServiceResult<Reservation>.Fail(timeError);

            var tableResult = await PickTableAsync(start, partySize, preferredTableId, null);
            if (!tableResult.Success) return ServiceResult<Reservation>.Fail(tableResult.Error!);
            var table = tableResult.Value!;

            var reservation = new Reservation
            {
                ReservationCode = GenerateCode("RSV"),
                CustomerId = customerId,
                RestaurantTableId = table.Id,
                StartTime = start,
                EndTime = end,
                PartySize = partySize,
                SpecialRequests = string.IsNullOrWhiteSpace(specialRequests) ? null : specialRequests.Trim(),
                Status = ReservationStatus.Pending
            };

            _db.Reservations.Add(reservation);
            _notifications.Queue(customerId, "Reservation received",
                $"Your booking {reservation.ReservationCode} for {partySize} on {start:ddd d MMM, h:mm tt} " +
                $"(Table {table.TableNumber}) is awaiting confirmation.",
                NotificationType.Reservation);

            await _db.SaveChangesAsync();
            reservation.RestaurantTable = table;
            return ServiceResult<Reservation>.Ok(reservation);
        }

        // CS8 / RS7 - modify. Pass customerId when a customer edits (checks ownership and
        // resets status to Pending); pass null when staff edits.
        public async Task<ServiceResult<Reservation>> ModifyAsync(int reservationId, DateTime start, int partySize,
                                                                  string? specialRequests, int? tableId = null,
                                                                  string? customerId = null)
        {
            var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId);
            if (reservation == null) return ServiceResult<Reservation>.Fail("Reservation not found.");

            if (customerId != null && reservation.CustomerId != customerId)
                return ServiceResult<Reservation>.Fail("You can only change your own reservations.");

            if (reservation.Status != ReservationStatus.Pending && reservation.Status != ReservationStatus.Confirmed)
                return ServiceResult<Reservation>.Fail($"A {reservation.Status} reservation can no longer be changed.");

            var sizeError = ValidatePartySize(partySize);
            if (sizeError != string.Empty) return ServiceResult<Reservation>.Fail(sizeError);

            var settings = await GetSettingsAsync();
            var end = start.AddMinutes(settings.ReservationDurationMinutes);

            var timeError = await ValidateTimeSlotAsync(start, end);
            if (timeError != null) return ServiceResult<Reservation>.Fail(timeError);

            // Keep the current table if it still fits and is free, otherwise find another one
            var wanted = tableId ?? reservation.RestaurantTableId;
            var tableResult = await PickTableAsync(start, partySize, wanted, reservation.Id);
            if (!tableResult.Success && tableId == null)
                tableResult = await PickTableAsync(start, partySize, null, reservation.Id);
            if (!tableResult.Success) return ServiceResult<Reservation>.Fail(tableResult.Error!);

            reservation.RestaurantTableId = tableResult.Value!.Id;
            reservation.StartTime = start;
            reservation.EndTime = end;
            reservation.PartySize = partySize;
            reservation.SpecialRequests = string.IsNullOrWhiteSpace(specialRequests) ? null : specialRequests.Trim();
            reservation.UpdatedAt = DateTime.Now;

            // A customer change needs to be re-confirmed by staff
            if (customerId != null) reservation.Status = ReservationStatus.Pending;

            _notifications.Queue(reservation.CustomerId, "Reservation updated",
                $"Booking {reservation.ReservationCode} is now {start:ddd d MMM, h:mm tt} for {partySize} " +
                $"(Table {tableResult.Value.TableNumber}). Status: {reservation.Status}.",
                NotificationType.Reservation);

            await _db.SaveChangesAsync();
            reservation.RestaurantTable = tableResult.Value;
            return ServiceResult<Reservation>.Ok(reservation);
        }

        // CS9 / RS8 - cancel. Pass customerId for a customer cancellation (ownership check).
        public async Task<ServiceResult> CancelAsync(int reservationId, string? customerId = null)
        {
            var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId);
            if (reservation == null) return ServiceResult.Fail("Reservation not found.");

            if (customerId != null && reservation.CustomerId != customerId)
                return ServiceResult.Fail("You can only cancel your own reservations.");

            return await ChangeStatusAsync(reservationId, ReservationStatus.Cancelled);
        }

        // ------------------------------------------------------------
        // STAFF ACTIONS
        // ------------------------------------------------------------

        // RS6 confirm, seat, complete, no-show, cancel - all go through the state machine
        public async Task<ServiceResult> ChangeStatusAsync(int reservationId, ReservationStatus newStatus)
        {
            var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId);
            if (reservation == null) return ServiceResult.Fail("Reservation not found.");

            if (!AllowedTransitions[reservation.Status].Contains(newStatus))
                return ServiceResult.Fail($"Cannot change a reservation from {reservation.Status} to {newStatus}.");

            reservation.Status = newStatus;
            reservation.UpdatedAt = DateTime.Now;

            var message = newStatus switch
            {
                ReservationStatus.Confirmed => $"Your booking {reservation.ReservationCode} on {reservation.StartTime:ddd d MMM, h:mm tt} is confirmed. See you soon!",
                ReservationStatus.Cancelled => $"Your booking {reservation.ReservationCode} on {reservation.StartTime:ddd d MMM, h:mm tt} has been cancelled.",
                ReservationStatus.Completed => $"Thanks for dining with us! You can now rate your experience for booking {reservation.ReservationCode}.",
                _ => null
            };

            if (message != null)
                _notifications.Queue(reservation.CustomerId, $"Reservation {newStatus.ToString().ToLower()}",
                    message, NotificationType.Reservation);

            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // RS4 - daily view for staff
        public Task<List<Reservation>> GetForDateAsync(DateTime date) =>
            _db.Reservations
               .Include(r => r.Customer)
               .Include(r => r.RestaurantTable).ThenInclude(t => t!.DiningArea)
               .Where(r => r.StartTime >= date.Date && r.StartTime < date.Date.AddDays(1))
               .OrderBy(r => r.StartTime)
               .ToListAsync();

        // CS12 / RS17 - history
        public Task<List<Reservation>> GetForCustomerAsync(string customerId) =>
            _db.Reservations
               .Include(r => r.RestaurantTable).ThenInclude(t => t!.DiningArea)
               .Where(r => r.CustomerId == customerId)
               .OrderByDescending(r => r.StartTime)
               .ToListAsync();

        // ------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------

        private async Task<ServiceResult<RestaurantTable>> PickTableAsync(DateTime start, int partySize,
                                                                          int? preferredTableId, int? ignoreReservationId)
        {
            var available = await GetAvailableTablesAsync(start, partySize, ignoreReservationId);

            if (preferredTableId != null)
            {
                var preferred = available.FirstOrDefault(t => t.Id == preferredTableId);
                return preferred != null
                    ? ServiceResult<RestaurantTable>.Ok(preferred)
                    : ServiceResult<RestaurantTable>.Fail("That table is not available for this time and party size.");
            }

            return available.Count > 0
                ? ServiceResult<RestaurantTable>.Ok(available[0])
                : ServiceResult<RestaurantTable>.Fail($"Sorry, no table for {partySize} is free at {start:h:mm tt} on {start:ddd d MMM}. Please try another time.");
        }

        internal static string GenerateCode(string prefix) =>
            $"{prefix}-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..5].ToUpper()}";

        private static string FormatTime(TimeSpan t) => DateTime.Today.Add(t).ToString("h:mm tt");
    }
}
