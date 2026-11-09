using Microsoft.AspNetCore.Http;
using WebApplication2.Services.Interfaces;

namespace WebApplication2.Services
{
    // Image upload for the admin pages.
    // Checks: file is not empty, at most 5 MB, has an image extension AND its first bytes
    // really are a JPG/PNG/GIF/WEBP (so a renamed .exe or script is rejected).
    // Files get a random name so uploads can never overwrite each other or escape the folder.
    public class ImageStorageService : IImageStorageService
    {
        public const long MaxBytes = 5 * 1024 * 1024;
        private const string UploadRoot = "uploads";

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly string[] AllowedFolders = { "menu", "areas" };

        private readonly IWebHostEnvironment _env;

        public ImageStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<ServiceResult<string>> SaveAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                return ServiceResult<string>.Fail("The selected image is empty.");
            if (file.Length > MaxBytes)
                return ServiceResult<string>.Fail("Image is too large. The limit is 5 MB.");
            if (!AllowedFolders.Contains(folder))
                return ServiceResult<string>.Fail("Unknown upload folder.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return ServiceResult<string>.Fail("Only JPG, PNG, GIF or WEBP images can be uploaded.");

            // Check the file signature ("magic bytes"), not just the name
            var header = new byte[12];
            await using (var check = file.OpenReadStream())
            {
                var read = await check.ReadAsync(header.AsMemory(0, header.Length));
                if (read < 4 || !LooksLikeImage(header))
                    return ServiceResult<string>.Fail("That file is not a valid image.");
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var dir = Path.Combine(webRoot, UploadRoot, folder);
            Directory.CreateDirectory(dir);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            await using (var output = new FileStream(Path.Combine(dir, fileName), FileMode.CreateNew))
            {
                await file.CopyToAsync(output);
            }

            return ServiceResult<string>.Ok($"/{UploadRoot}/{folder}/{fileName}");
        }

        public void Delete(string? imagePath)
        {
            if (!IsOwnUpload(imagePath)) return;

            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadsDir = Path.GetFullPath(Path.Combine(webRoot, UploadRoot));
            var full = Path.GetFullPath(Path.Combine(webRoot, imagePath!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

            // Never delete anything outside wwwroot/uploads
            if (full.StartsWith(uploadsDir, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
                File.Delete(full);
        }

        public bool IsValidImagePath(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath)) return true;
            var p = imagePath.Trim();
            if (IsOwnUpload(p)) return true;
            return Uri.TryCreate(p, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static bool IsOwnUpload(string? path) =>
            !string.IsNullOrWhiteSpace(path)
            && path.StartsWith($"/{UploadRoot}/", StringComparison.OrdinalIgnoreCase)
            && !path.Contains("..");

        private static bool LooksLikeImage(byte[] h) =>
            (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) ||                                   // JPG
            (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47) ||                   // PNG
            (h[0] == 0x47 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x38) ||                   // GIF
            (h[0] == 0x52 && h[1] == 0x49 && h[2] == 0x46 && h[3] == 0x46 &&                    // WEBP = "RIFF....WEBP"
             h[8] == 0x57 && h[9] == 0x45 && h[10] == 0x42 && h[11] == 0x50);
    }
}
