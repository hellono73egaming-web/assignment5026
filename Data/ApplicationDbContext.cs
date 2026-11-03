using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;

namespace WebApplication2.Data
{
    // REPLACES the ApplicationDbContext that Visual Studio generated in /Data.
    // Inherits IdentityDbContext so the AspNetUsers / AspNetRoles tables are included.
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Each subclass gets its own DbSet, but they all live in AspNetUsers (TPH)
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Staff> StaffMembers => Set<Staff>();
        public DbSet<Admin> Admins => Set<Admin>();

        public DbSet<RestaurantSetting> RestaurantSettings => Set<RestaurantSetting>();
        public DbSet<OperatingHour> OperatingHours => Set<OperatingHour>();
        public DbSet<DiningArea> DiningAreas => Set<DiningArea>();
        public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
        public DbSet<Announcement> Announcements => Set<Announcement>();
        public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
        public DbSet<MenuItem> MenuItems => Set<MenuItem>();
        public DbSet<Promotion> Promotions => Set<Promotion>();
        public DbSet<Reservation> Reservations => Set<Reservation>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Feedback> Feedbacks => Set<Feedback>();
        public DbSet<Notification> Notifications => Set<Notification>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);   // must stay first - configures Identity tables

            // INHERITANCE: Table-Per-Hierarchy with a "UserType" column
            builder.Entity<ApplicationUser>()
                .HasDiscriminator<string>("UserType")
                .HasValue<Customer>("Customer")
                .HasValue<Staff>("Staff")
                .HasValue<Admin>("Admin");

            builder.Entity<Staff>().HasIndex(s => s.EmployeeId);

            // Store enums as readable text in SQL Server instead of numbers
            builder.Entity<Reservation>().Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            builder.Entity<Order>().Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            builder.Entity<Order>().Property(o => o.OrderType).HasConversion<string>().HasMaxLength(20);
            builder.Entity<Payment>().Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            builder.Entity<Payment>().Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
            builder.Entity<MenuItem>().Property(m => m.ItemType).HasConversion<string>().HasMaxLength(20);
            builder.Entity<Notification>().Property(n => n.Type).HasConversion<string>().HasMaxLength(20);

            builder.Entity<RestaurantSetting>().Property(s => s.ServiceTaxPercent).HasPrecision(5, 2);
            builder.Entity<Promotion>().Property(p => p.DiscountPercent).HasPrecision(5, 2);

            // Unique constraints
            builder.Entity<RestaurantTable>().HasIndex(t => t.TableNumber).IsUnique();
            builder.Entity<Reservation>().HasIndex(r => r.ReservationCode).IsUnique();
            builder.Entity<Order>().HasIndex(o => o.OrderNumber).IsUnique();

            // Index that speeds up the availability check
            builder.Entity<Reservation>().HasIndex(r => new { r.RestaurantTableId, r.StartTime, r.EndTime });

            // One-to-one Order <-> Payment
            builder.Entity<Payment>()
                .HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // SQL Server rejects "multiple cascade paths", so everything that points
            // back to a user / reservation / menu item uses Restrict (no auto-delete).
            // Records like orders and payments should never silently disappear anyway.
            builder.Entity<Reservation>()
                .HasOne(r => r.Customer).WithMany(u => u.Reservations)
                .HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Reservation>()
                .HasOne(r => r.RestaurantTable).WithMany(t => t.Reservations)
                .HasForeignKey(r => r.RestaurantTableId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Order>()
                .HasOne(o => o.Customer).WithMany(u => u.Orders)
                .HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Order>()
                .HasOne(o => o.Reservation).WithMany(r => r.Orders)
                .HasForeignKey(o => o.ReservationId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<OrderItem>()
                .HasOne(oi => oi.MenuItem).WithMany(m => m.OrderItems)
                .HasForeignKey(oi => oi.MenuItemId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MenuItem>()
                .HasOne(m => m.MenuCategory).WithMany(c => c.MenuItems)
                .HasForeignKey(m => m.MenuCategoryId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<RestaurantTable>()
                .HasOne(t => t.DiningArea).WithMany(a => a.Tables)
                .HasForeignKey(t => t.DiningAreaId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Promotion>()
                .HasOne(p => p.MenuItem).WithMany(m => m.Promotions)
                .HasForeignKey(p => p.MenuItemId).OnDelete(DeleteBehavior.SetNull);

            builder.Entity<Feedback>()
                .HasOne(f => f.Customer).WithMany(u => u.Feedbacks)
                .HasForeignKey(f => f.CustomerId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Feedback>()
                .HasOne(f => f.Order).WithMany()
                .HasForeignKey(f => f.OrderId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Feedback>()
                .HasOne(f => f.Reservation).WithMany()
                .HasForeignKey(f => f.ReservationId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Notification>()
                .HasOne(n => n.User).WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
