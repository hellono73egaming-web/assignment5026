using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace WebApplication2.Models
{
    // BASE CLASS (abstract - you can never create a plain "ApplicationUser").
    // Inherits IdentityUser, which already gives: Id, UserName, Email, PasswordHash, PhoneNumber.
    // All three user types share these fields; each subclass adds its own.
    //
    // EF Core stores all subclasses in ONE table (AspNetUsers) using
    // "Table-Per-Hierarchy" (TPH). A "UserType" discriminator column says which subclass a row is.
    public abstract class ApplicationUser : IdentityUser
    {
        [Required, StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        // POLYMORPHISM: each subclass returns its own role name.
        // Used when assigning the Identity role and for display.
        [NotMapped]
        public abstract string RoleName { get; }
    }

    // Registers through the public Register page.
    public class Customer : ApplicationUser
    {
        [StringLength(500)]
        [Display(Name = "Dining Preferences")]
        public string? DiningPreferences { get; set; }   // e.g. "vegetarian, window seat"

        public DateTime? DateOfBirth { get; set; }

        public int LoyaltyPoints { get; set; }

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

        public override string RoleName => Roles.Customer;
    }

    // Created by an Admin (no self-registration).
    public class Staff : ApplicationUser
    {
        [StringLength(20)]
        [Display(Name = "Employee ID")]
        public string EmployeeId { get; set; } = string.Empty;   // e.g. "EMP001"

        [StringLength(50)]
        public string Position { get; set; } = "Waiter";          // Waiter, Cashier, Chef, Manager

        [Display(Name = "Hire Date")]
        public DateTime HireDate { get; set; } = DateTime.Today;

        public override string RoleName => Roles.Staff;
    }

    // Seeded at startup / created by another Admin (no self-registration).
    public class Admin : ApplicationUser
    {
        public DateTime? LastLoginAt { get; set; }

        public override string RoleName => Roles.Admin;
    }
}
