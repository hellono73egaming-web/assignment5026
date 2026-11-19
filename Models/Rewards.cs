using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models
{
    public enum VoucherType
    {
        FixedAmount,     // e.g. RM 5 off
        Percent,         // e.g. 15% off
        BuyOneFreeOne    // buy 2 of the chosen dish, 1 is free
    }

    // A reward in the catalogue that customers can buy with loyalty points
    // (e.g. "RM 5 off" for 100 points).
    public class Voucher
    {
        public int Id { get; set; }

        [Required, StringLength(80)]
        public string Name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Description { get; set; }

        [Range(1, 100000)]
        public int PointsCost { get; set; }

        public VoucherType Type { get; set; } = VoucherType.FixedAmount;

        // RM amount (FixedAmount) or percentage (Percent); not used for Buy 1 Free 1
        [Range(0, 10000)]
        public decimal Value { get; set; }

        // The dish for Buy 1 Free 1
        public int? MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        // Picture shown on the Rewards page (uploaded file or link)
        [StringLength(500)]
        public string? ImageUrl { get; set; }

        // Order must be at least this much (after promotions) for the voucher to work
        [Range(0, 10000)]
        public decimal MinSpend { get; set; }

        // How long a bought voucher stays usable
        [Range(1, 365)]
        public int ValidDays { get; set; } = 30;

        public bool IsActive { get; set; } = true;

        [NotMapped]
        public string Label => Type switch
        {
            VoucherType.FixedAmount => $"RM {Value:N2} off",
            VoucherType.Percent => $"{Value:0.##}% off",
            _ => "Buy 1 Free 1"
        };
    }

    // A voucher a customer has bought with their points: one unique code, used once.
    public class CustomerVoucher
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; } = string.Empty;   // e.g. "ART-7K2M9Q"

        [Required]
        public string CustomerId { get; set; } = string.Empty;
        public Customer? Customer { get; set; }

        public int VoucherId { get; set; }
        public Voucher? Voucher { get; set; }

        public int PointsSpent { get; set; }
        public DateTime RedeemedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; }

        // Set when the code is used on an order; cleared again if that order is cancelled
        public DateTime? UsedAt { get; set; }
        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        [NotMapped]
        public string Status => UsedAt != null ? "Used" : ExpiresAt < DateTime.Now ? "Expired" : "Available";
    }
}
