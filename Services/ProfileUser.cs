namespace WebApplication2.Services
{
    public class ProfileUser
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Customer specific class elements
        public string? DiningPreferences { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? LoyaltyPoints { get; set; }

        // Staff specific class elements
        public string? EmployeeId { get; set; }
        public string? Position { get; set; }
        public DateTime? HireDate { get; set; }

        // Admin specific class elements
        public DateTime? LastLoginAt { get; set; }

        public bool IsCustomer => RoleName == Models.Roles.Customer;
        public bool IsStaff => RoleName == Models.Roles.Staff;
        public bool IsAdmin => RoleName == Models.Roles.Admin;



    }

    public class UpdateProfileRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }

        // Only applied when the user is a Customer
        public string? DiningPreferences { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }
}
    
