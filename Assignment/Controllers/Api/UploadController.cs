using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Assignment.Controllers.Api
{
    [ApiController]
    [Route("api/uploads")]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private static readonly string[] AllowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private static readonly string[] AllowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        private readonly IWebHostEnvironment _environment;

        public UploadController(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        [HttpPost("images")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxFileSize)]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file ảnh để tải lên." });
            }

            if (file.Length > MaxFileSize)
            {
                return BadRequest(new { message = "Ảnh vượt quá dung lượng cho phép (tối đa 5MB)." });
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                return BadRequest(new { message = "Định dạng file ảnh không hợp lệ." });
            }

            extension = extension.ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Chỉ hỗ trợ các định dạng JPG, PNG, GIF hoặc WEBP." });
            }

            var contentType = file.ContentType?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(contentType) && !AllowedContentTypes.Contains(contentType))
            {
                return BadRequest(new { message = "Chỉ hỗ trợ các định dạng JPG, PNG, GIF hoặc WEBP." });
            }

            var uploadsRoot = Path.Combine(_environment.WebRootPath ?? string.Empty, "uploads", "images");
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            try
            {
                await using var stream = System.IO.File.Create(filePath);
                await file.CopyToAsync(stream);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Không thể lưu ảnh. Vui lòng thử lại sau." });
            }

            var relativePath = $"/uploads/images/{fileName}";
            string url;
            if (!string.IsNullOrEmpty(Request.Scheme) && Request.Host.HasValue)
            {
                var basePath = $"{Request.Scheme}://{Request.Host}{Request.PathBase}".TrimEnd('/');
                url = $"{basePath}{relativePath}";
            }
            else
            {
                url = relativePath;
            }

            return Ok(new { url });
        }
    }
}
