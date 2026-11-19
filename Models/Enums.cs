namespace WebApplication2.Models
{
    public enum ReservationStatus
    {
        Pending,     // customer booked, waiting for staff
        Confirmed,   // staff confirmed
        Seated,      // customer arrived
        Completed,
        Cancelled,
        NoShow
    }

    public enum OrderStatus
    {
        Pending,       // placed, not yet accepted by kitchen
        Preparing,
        ReadyToServe,
        Served,
        Completed,
        Cancelled
    }

    // CS10 / CS11 - food vs beverage orders
    public enum MenuItemType
    {
        Food,
        Beverage
    }

    public enum OrderType
    {
        DineIn,
        Takeaway
    }

    public enum PaymentStatus
    {
        Pending,
        Paid,
        Failed,
        Refunded
    }

    public enum PaymentMethod
    {
        Cash,
        Card,
        EWallet,
        OnlineBanking
    }

    public enum PromotionDiscountType
    {
        Percent,
        Fixed
    }

    public enum AnnouncementAudience
    {
        Customers,
        Staff,
        All
    }

    public enum NotificationType
    {
        Reservation,
        Order,
        Payment,
        Promotion,
        General
    }

    public static class Roles
    {
        public const string Admin = "Administrator";
        public const string Staff = "Staff";
        public const string Customer = "Customer";
    }
}
