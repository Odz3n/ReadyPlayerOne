using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.DTOs.Images;
using ReadyPlayerOne.Storage;
using System.Security.Claims;

namespace ReadyPlayerOne.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/users/me/avatar")]
    public class UserAvatarController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IBlobImageStorage _images;
        private readonly ILogger<UserAvatarController> _logger;

        public UserAvatarController(
            ApplicationDbContext db,
            IBlobImageStorage images,
            ILogger<UserAvatarController> logger)
        {
            _db = db;
            _images = images;
            _logger = logger;
        }

        [HttpPut]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6_000_000)]
        public async Task<ActionResult<AvatarImageResponse>> Upload(
        [FromForm] IFormFile? file, CancellationToken token)
        {
            string? userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var old = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.AvatarUrl })
                .SingleOrDefaultAsync(token);
            if (old == null)
                return NotFound("User not found.");
            if (file == null)
                return BadRequest("Upload a file in multipart field 'file'.");

            var prefix = $"users/{userId}";
            string newUrl;
            try
            {
                newUrl = await _images.UploadAsync(BlobImageStorage.AvatarContainer, prefix, file, token);
            }
            catch (ImageValidationException ex)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid image", Detail = ex.Message, Status = 400 });
            }

            int updated;
            try
            {
                updated = await _db.Users
                    .Where(u => u.Id == userId && u.AvatarUrl == old.AvatarUrl)
                    .ExecuteUpdateAsync(s => s.SetProperty(u => u.AvatarUrl, newUrl), token);
            }
            catch
            {
                await BlobCleanup.TryDeleteAsync(_images, _logger, BlobImageStorage.AvatarContainer, prefix, newUrl);
                throw;
            }

            if (updated == 0)
            {
                await BlobCleanup.TryDeleteAsync(_images, _logger, BlobImageStorage.AvatarContainer, prefix, newUrl);
                return Conflict("Avatar changed in another request. Try again.");
            }

            await BlobCleanup.TryDeleteAsync(_images, _logger, BlobImageStorage.AvatarContainer, prefix, old.AvatarUrl);
            return Ok(new AvatarImageResponse(newUrl));
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(CancellationToken token)
        {
            string? userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var old = await _db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.AvatarUrl })
                .SingleOrDefaultAsync(token);
            if (old == null)
                return NotFound("User not found.");
            if (old.AvatarUrl == null)
                return NoContent();

            var updated = await _db.Users
                .Where(u => u.Id == userId && u.AvatarUrl == old.AvatarUrl)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.AvatarUrl, (string?)null), token);
            if (updated == 0)
                return Conflict("Avatar changed in another request. Try again.");

            await BlobCleanup.TryDeleteAsync(_images, _logger, BlobImageStorage.AvatarContainer, $"users/{userId}", old.AvatarUrl);
            return NoContent();
        }

        private string? GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub");
        }
    }
}
