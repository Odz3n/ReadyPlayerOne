using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.DTOs.Images;
using ReadyPlayerOne.Storage;

namespace ReadyPlayerOne.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/games/{gameId:guid}/cover")]
    public class GameCoverController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IBlobImageStorage _images;
        private readonly ILogger<GameCoverController> _logger;

        public GameCoverController(
            ApplicationDbContext db,
            IBlobImageStorage images,
            ILogger<GameCoverController> logger)
        {
            _db = db;
            _images = images;
            _logger = logger;
        }

        [HttpPut]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(6000000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 6000000)]
        public async Task<ActionResult<GameCoverImageResponse>> Upload(
            Guid gameId,
            [FromForm] IFormFile? file,
            CancellationToken token)
        {
            var old = await _db.Games.AsNoTracking()
                .Where(g => g.Id == gameId)
                .Select(g => new { g.CoverImageUrl })
                .SingleOrDefaultAsync(token);
            if (old == null)
                return NotFound("Game not found.");
            if (file == null)
                return BadRequest("Upload a file in multipart field 'file'.");

            var prefix = $"games/{gameId:D}";
            string newUrl;
            try
            {
                newUrl = await _images.UploadAsync(BlobImageStorage.GameCoverContainer, prefix, file, token);
            }
            catch (ImageValidationException ex)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid image", Detail = ex.Message, Status = 400 });
            }

            int updated;
            try
            {
                updated = await _db.Games
                    .Where(g => g.Id == gameId && g.CoverImageUrl == old.CoverImageUrl)
                    .ExecuteUpdateAsync(s => s.SetProperty(g => g.CoverImageUrl, newUrl), token);
            }
            catch
            {
                await BlobCleanup.TryDeleteAsync(_images, _logger, BlobImageStorage.GameCoverContainer, prefix, newUrl);
                throw;
            }

            if (updated == 0)
            {
                await BlobCleanup.TryDeleteAsync(_images, _logger, BlobImageStorage.GameCoverContainer, prefix, newUrl);
                return Conflict("Game cover changed in another request. Try again.");
            }

            await BlobCleanup.TryDeleteAsync(_images, _logger, BlobImageStorage.GameCoverContainer, prefix, old.CoverImageUrl);
            return Ok(new GameCoverImageResponse(newUrl));
        }

        [HttpDelete]
        public async Task<IActionResult> Delete(
            Guid gameId,
            CancellationToken token)
        {
            var old = await _db.Games.AsNoTracking()
                .Where(g => g.Id == gameId)
                .Select(g => new { g.CoverImageUrl })
                .SingleOrDefaultAsync(token);
            if (old == null)
                return NotFound("Game not found.");
            if (old.CoverImageUrl == null)
                return NoContent();

            var updated = await _db.Games
                .Where(g => g.Id == gameId && g.CoverImageUrl == old.CoverImageUrl)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.CoverImageUrl, (string?)null), token);
            if (updated == 0)
                return Conflict("Game cover changed in another request. Try again.");

            await BlobCleanup.TryDeleteAsync(_images, _logger, BlobImageStorage.GameCoverContainer, $"games/{gameId:D}", old.CoverImageUrl);
            return NoContent();
        }
    }
}
