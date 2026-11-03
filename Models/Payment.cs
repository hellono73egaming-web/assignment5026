using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models
{
    // Staff 2f, Customer 3f, Intermediate 3/4 (receipts, payment tracking)
    public class Payment
    {
        public int Id { get; set; }

        public int OrderId { get; set; }          // one payment per order
        public Order? Order { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        public PaymentMethod Method { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        [StringLength(30)]
        [Display(Name = "Receipt No.")]
        public string? ReceiptNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Transaction Ref")]
        public string? TransactionReference { get; set; }   // from the (simulated) payment gateway

        public DateTime? PaidAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
