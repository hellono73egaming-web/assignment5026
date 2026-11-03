using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    // Single-row table holding general restaurant settings (Admin 1b, 1g)
    public class RestaurantSetting
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string RestaurantName { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Address { get; set; }

        [Phone, StringLength(20)]
        public string? Phone { get; set; }

        [EmailAddress, StringLength(100)]
        public string? Email { get; set; }

        // How long one reservation blocks a table, used for availability checking
        [Range(30, 300)]
        public int ReservationDurationMinutes { get; set; } = 120;

        [Range(0, 100)]
        public decimal ServiceTaxPercent { get; set; } = 6;
    }

    // Admin 1b - operating hours, one row per day of week
    public class OperatingHour
    {
        public int Id { get; set; }

        public DayOfWeek Day { get; set; }

        public TimeSpan OpenTime { get; set; }

        public TimeSpan CloseTime { get; set; }

        public bool IsClosed { get; set; }
    }

    // Admin 1b - dining areas, e.g. "Main Hall", "Outdoor", "VIP Room"
    public class DiningArea
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Description { get; set; }

        public ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();
    }

    // Admin 1b - physical tables
    public class RestaurantTable
    {
        public int Id { get; set; }

        [Required, StringLength(10)]
        [Display(Name = "Table No.")]
        public string TableNumber { get; set; } = string.Empty;   // e.g. "A1"

        [Range(1, 20)]
        public int Capacity { get; set; }

        public bool IsActive { get; set; } = true;

        public int DiningAreaId { get; set; }
        public DiningArea? DiningArea { get; set; }

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }

    // Admin 1g - announcements / promotional campaigns shown to customers
    public class Announcement
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(2000)]
        public string Content { get; set; } = string.Empty;

        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime? EndDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
