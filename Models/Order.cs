using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models
{
    // Customer 3e, Staff 2d/2e
    public class Order
    {
        public int Id { get; set; }

        [StringLength(30)]
        public string OrderNumber { get; set; } = string.Empty;   // e.g. "ORD-20261102-0012"

        [Required]
        public string CustomerId { get; set; } = string.Empty;
        public Customer? Customer { get; set; }

        // Optional: order linked to a table booking
        public int? ReservationId { get; set; }
        public Reservation? Reservation { get; set; }

        public OrderType OrderType { get; set; } = OrderType.DineIn;

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [StringLength(500)]
        public string? Notes { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Discount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Tax { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Total { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public Payment? Payment { get; set; }
    }

    // One line in an order
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; }

        // Price copied at time of ordering, so later price changes don't alter old orders
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [StringLength(200)]
        public string? Remarks { get; set; }   // e.g. "no onion"

        [NotMapped]
        public decimal LineTotal => UnitPrice * Quantity;
    }
}
