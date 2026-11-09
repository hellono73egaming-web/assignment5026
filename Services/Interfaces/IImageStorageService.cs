using Microsoft.AspNetCore.Http;

namespace WebApplication2.Services.Interfaces
{
    // Saves images uploaded by the admin (menu items, dining areas, promotions) into wwwroot/uploads
    // and returns the web path (e.g. /uploads/menu/abc123.jpg) to store in the database.
    public interface IImageStorageService
    {
        Task<ServiceResult<string>> SaveAsync(IFormFile file, string folder);

        // Deletes a previously uploaded file. Links to other websites are ignored.
        void Delete(string? imagePath);

        // True for a full http/https link or a path to one of our own uploads.
        bool IsValidImagePath(string? imagePath);
    }
}
