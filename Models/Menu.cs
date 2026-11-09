using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication2.Models
{
    // Admin 1b - e.g. "Appetisers", "Mains", "Drinks"
    public class MenuCategory
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Description { get; set; }

        public int DisplayOrder { get; set; }

        public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }

    // Admin 1d, Customer 3c
    public class MenuItem
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, 10000)]
        public decimal Price { get; set; }

        [StringLength(300)]
        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [Display(Name = "Type")]
        public MenuItemType ItemType { get; set; } = MenuItemType.Food;

        [Display(Name = "Available")]
        public bool IsAvailable { get; set; } = true;

        // Simple tags used by the recommendation system, e.g. "spicy,vegetarian,chicken"
        [StringLength(200)]
        public string? Tags { get; set; }

        public int MenuCategoryId { get; set; }
        public MenuCategory? MenuCategory { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
    }

    // Admin 1d - promotional offers. MenuItemId null = applies to whole order.
    public class Promotion
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Percent: DiscountPercent is used (e.g. 15 = 15% off)
        // Fixed:   DiscountAmount is used (RM off each item, or off the whole order)
        public PromotionDiscountType DiscountType { get; set; } = PromotionDiscountType.Percent;

        [Range(0, 100)]
        [Display(Name = "Discount %")]
        public decimal DiscountPercent { get; set; }

        [Range(0, 10000)]
        [Display(Name = "Discount (RM)")]
        public decimal DiscountAmount { get; set; }

        // Optional: when set, the customer must enter this code at checkout ("Auto" when empty)
        [StringLength(30)]
        public string? PromoCode { get; set; }

        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

        public bool IsActive { get; set; } = true;

        // Scope: one menu item, one category, or (both null) the whole order
        public int? MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        public int? MenuCategoryId { get; set; }
        public MenuCategory? MenuCategory { get; set; }

        [NotMapped]
        public bool IsCurrentlyValid =>
            IsActive && DateTime.Today >= StartDate.Date && DateTime.Today <= EndDate.Date;

        // "15% off" / "RM 10.00 off"
        [NotMapped]
        public string DiscountLabel => DiscountType == PromotionDiscountType.Fixed
            ? $"RM {DiscountAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} off"
            : $"{DiscountPercent:0.##}% off";
    }
}
