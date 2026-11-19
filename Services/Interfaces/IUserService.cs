using WebApplication2.Models;

namespace WebApplication2.Services.Interfaces
{
    // AD3 / Admin c: manage customer and staff accounts, registration and role assignment.
    public interface IUserService
    {
        Task<List<CustomerSummary>> GetCustomersAsync();
        Task<List<Staff>> GetStaffAsync();

        Task<ServiceResult> CreateStaffAsync(string fullName, string email, string? phone, string position, string password);
        Task<ServiceResult> UpdateStaffAsync(string id, string fullName, string? phone, string position);
        Task<ServiceResult> SetActiveAsync(string id, bool active, string currentUserId);
        Task<ServiceResult> DeleteAsync(string id, string currentUserId);

        // A customer who forgot their password contacts the restaurant; the admin sets a
        // temporary password and reads it to them. Returns the temporary password.
        Task<ServiceResult<string>> ResetCustomerPasswordAsync(string customerId);
    }
}
