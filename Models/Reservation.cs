using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    // Customer 3d, Staff 2b/2c
    public class Reservation
    {
        public int Id { get; set; }

        // Human-friendly code shown on the confirmation, e.g. "RSV-20261102-0007"
        [StringLength(30)]
        public string ReservationCode { get; set; } = string.Empty;

        [Required]
        public string CustomerId { get; set; } = string.Empty;
        public Customer? Customer { get; set; }

        public int RestaurantTableId { get; set; }
        public RestaurantTable? RestaurantTable { get; set; }

        [Display(Name = "Start")]
        public DateTime StartTime { get; set; }

        // StartTime + RestaurantSetting.ReservationDurationMinutes.
        // Two reservations clash on the same table if: newStart < existingEnd && newEnd > existingStart
        [Display(Name = "End")]
        public DateTime EndTime { get; set; }

        [Range(1, 20)]
        [Display(Name = "Party Size")]
        public int PartySize { get; set; }

        [StringLength(500)]
        [Display(Name = "Special Requests")]
        public string? SpecialRequests { get; set; }

        public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
