using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Genres;
using ReadyPlayerOne.Interfaces;

namespace ReadyPlayerOne.Controllers
{
    [ApiController]
    [Route("api/genres")]
    public class GenresController : ControllerBase
    {
        private readonly IGenreService _genreService;

        public GenresController(IGenreService genreService)
        {
            _genreService = genreService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<GenreResponse>>> GetAll(
            [FromQuery] PaginationRequest pagination,
            CancellationToken token)
        {
            var result = await _genreService.GetAllAsync(
                pagination, token);

            return Ok(result);
        }

        [HttpGet("{id:guid}", Name = "GetGenreById")]
        [AllowAnonymous]
        public async Task<ActionResult<GenreResponse>> GetById(
            Guid id,
            CancellationToken token)
        {
            var genre = await _genreService.GetByIdAsync(id, token);
            if (genre is null)
                return NotFound();

            return Ok(genre);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<GenreResponse>> Create(
            [FromBody] CreateGenreRequest request,
            CancellationToken token)
        {
            var genre = await _genreService.CreateAsync(request, token);

            return CreatedAtRoute(
                "GetGenreById",
                new { id = genre.Id },
                genre);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateGenreRequest request,
            CancellationToken token)
        {
            bool updated = await _genreService.UpdateAsync(id, request, token);

            if (!updated)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(
            Guid id,
            CancellationToken token)
        {
            bool deleted = await _genreService.DeleteAsync(id, token);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
