namespace WebApplication2.Services.Interfaces
{
    // CS3, CS4, RS2, RS3 - view and edit your OWN profile.
    // userId always comes from the logged-in session, never from the form.
    public interface IProfileService
    {
        Task<ProfileUser?> GetProfileAsync(string userId);
        Task<ServiceResult> UpdateProfileAsync(string userId, UpdateProfileRequest request);
        Task<bool> HasPasswordAsync(string userId);
        Task<ServiceResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<ServiceResult> SetPasswordAsync(string userId, string newPassword);   // for Google/Microsoft sign-ups
    }
}
