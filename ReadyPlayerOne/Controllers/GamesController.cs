using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Games;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GamesController : ControllerBase
    {
        private readonly IGameService _gameService;
        public GamesController(IGameService gameService)
        {
            _gameService = gameService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<GameResponse>>> GetAllAsync(
            [FromQuery] PaginationRequest pagination,
            CancellationToken token)
        {
            var result = await _gameService.GetAllAsync(pagination, token);

            return Ok(result);
        }

        [HttpGet("{id:guid}", Name = "GetGameById")]
        [AllowAnonymous]
        public async Task<ActionResult<GameResponse>> GetByIdAsync(
            Guid id,
            CancellationToken token)
        {
            var game = await _gameService.GetByIdAsync(id, token);
            if (game == null)
                return NotFound();

            return Ok(game);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<GameResponse>> CreateAsync(
            [FromBody] CreateGameRequest request,
            CancellationToken token)
        {
            var game = await _gameService.CreateAsync(request, token);

            return CreatedAtAction("GetGameById", new { id = game.Id }, game);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateAsync(
            Guid id,
            [FromBody] UpdateGameRequest request,
            CancellationToken token)
        {
            var updated = await _gameService.UpdateAsync(id, request, token);
            if (!updated)
                return NotFound();

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAsync(
            Guid id,
            CancellationToken token)
        {
            var deleted = await _gameService.DeleteAsync(id, token);
            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
