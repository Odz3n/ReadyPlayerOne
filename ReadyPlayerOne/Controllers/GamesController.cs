using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Games;
using ReadyPlayerOne.Interfaces;

namespace ReadyPlayerOne.Controllers;

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
        [FromQuery] GameQueryRequest request,
        CancellationToken token)
    {
        var result = await _gameService.GetAllAsync(request, token);
        return Ok(result);
    }

    [HttpGet("{id:guid}", Name = "GetGameById")]
    [AllowAnonymous]
    public async Task<ActionResult<GameResponse>> GetByIdAsync(
        Guid id,
        CancellationToken token)
    {
        var game = await _gameService.GetByIdAsync(id, token);

        if (game is null)
            return NotFound();

        return Ok(game);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<GameResponse>> CreateAsync(
        [FromBody] CreateGameRequest request,
        CancellationToken token)
    {
        try
        {
            var game = await _gameService.CreateAsync(request, token);
            return CreatedAtRoute("GetGameById", new { id = game.Id }, game);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid game data",
                Detail = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid genre",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "Game conflict",
                Detail = ex.Message
            });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateGameRequest request,
        CancellationToken token)
    {
        try
        {
            var updated = await _gameService.UpdateAsync(id, request, token);

            if (!updated)
                return NotFound();

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid game data",
                Detail = ex.Message
            });
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid genre",
                Detail = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "Game conflict",
                Detail = ex.Message
            });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken token)
    {
        try
        {
            var deleted = await _gameService.DeleteAsync(id, token);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "Cannot delete game",
                Detail = ex.Message
            });
        }
    }
}