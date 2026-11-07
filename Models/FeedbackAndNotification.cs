using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    // Customer 3h - rating for a completed dining experience
    public class Feedback
    {
        public int Id { get; set; }

        [Required]
        public string CustomerId { get; set; } = string.Empty;
        public Customer? Customer { get; set; }

        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        public int? ReservationId { get; set; }
        public Reservation? Reservation { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Review management: the restaurant's public reply to this review
        [StringLength(500)]
        public string? AdminReply { get; set; }
        public DateTime? RepliedAt { get; set; }
    }

    // Customer 3i, Advanced 5 - in-system notifications
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        [Required, StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(500)]
        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; } = NotificationType.General;

        [StringLength(200)]
        public string? Link { get; set; }   // e.g. "/Customer/Orders/Details?id=5"

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
