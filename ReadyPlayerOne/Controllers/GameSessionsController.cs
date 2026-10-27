using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.GameSessions;
using ReadyPlayerOne.Interfaces;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ReadyPlayerOne.Controllers
{
    [ApiController]
    [Route("api/game-sessions")]
    public class GameSessionsController : ControllerBase
    {
        private readonly IGameSessionService _sessionService;

        public GameSessionsController(IGameSessionService sessionService)
        {
            _sessionService = sessionService;
        }

        private string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? throw new UnauthorizedAccessException("User ID not found in token.");

        private bool IsAdmin => User.IsInRole("Admin");

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<GameSessionResponse>>> GetAll(
            [FromQuery] GameSessionQueryRequest request, CancellationToken token)
        {
            return Ok(await _sessionService.GetAllAsync(request, token));
        }

        [HttpGet("{id:guid}", Name = "GetGameSessionById")]
        [AllowAnonymous]
        public async Task<ActionResult<GameSessionResponse>> GetById(Guid id, CancellationToken token)
        {
            var session = await _sessionService.GetByIdAsync(id, token);
            return session is null ? NotFound() : Ok(session);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<GameSessionResponse>> Create(
            [FromBody] CreateGameSessionRequest request, CancellationToken token)
        {
            var session = await _sessionService.CreateAsync(request, CurrentUserId, token);
            return CreatedAtRoute("GetGameSessionById", new { id = session.Id }, session);
        }

        [HttpPut("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGameSessionRequest request, CancellationToken token)
        {
            var updated = await _sessionService.UpdateAsync(id, request, CurrentUserId, IsAdmin, token);
            return updated ? NoContent() : NotFound();
        }

        [HttpDelete("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid id, CancellationToken token)
        {
            var deleted = await _sessionService.DeleteAsync(id, CurrentUserId, IsAdmin, token);
            return deleted ? NoContent() : NotFound();
        }

        [HttpPost("{id:guid}/join")]
        [Authorize]
        public async Task<IActionResult> Join(Guid id, CancellationToken token)
        {
            await _sessionService.JoinAsync(id, CurrentUserId, token);
            return NoContent();
        }

        [HttpPost("{id:guid}/leave")]
        [Authorize]
        public async Task<IActionResult> Leave(Guid id, CancellationToken token)
        {
            await _sessionService.LeaveAsync(id, CurrentUserId, token);
            return NoContent();
        }

        [HttpPut("{id:guid}/ready")]
        [Authorize]
        public async Task<IActionResult> SetReady(Guid id, [FromBody] bool isReady, CancellationToken token)
        {
            await _sessionService.SetReadyAsync(id, CurrentUserId, isReady, token);
            return NoContent();
        }

        [HttpPost("{id:guid}/teams")]
        [Authorize]
        public async Task<ActionResult<TeamResponse>> CreateTeam(
            Guid id, [FromBody] CreateTeamRequest request, CancellationToken token)
        {
            var team = await _sessionService.CreateTeamAsync(id, request, CurrentUserId, IsAdmin, token);
            return Ok(team);
        }

        [HttpPost("{id:guid}/teams/{teamId:guid}/join")]
        [Authorize]
        public async Task<IActionResult> JoinTeam(Guid id, Guid teamId, CancellationToken token)
        {
            await _sessionService.JoinTeamAsync(id, teamId, CurrentUserId, token);
            return NoContent();
        }

        [HttpPost("{id:guid}/teams/leave")]
        [Authorize]
        public async Task<IActionResult> LeaveTeam(Guid id, CancellationToken token)
        {
            await _sessionService.LeaveTeamAsync(id, CurrentUserId, token);
            return NoContent();
        }

        [HttpPost("{id:guid}/start")]
        [Authorize]
        public async Task<IActionResult> Start(Guid id, CancellationToken token)
        {
            await _sessionService.StartAsync(id, CurrentUserId, IsAdmin, token);
            return NoContent();
        }

        [HttpPost("{id:guid}/complete")]
        [Authorize]
        public async Task<IActionResult> Complete(Guid id, CancellationToken token)
        {
            await _sessionService.CompleteAsync(id, CurrentUserId, IsAdmin, token);
            return NoContent();
        }

        [HttpPost("{id:guid}/cancel")]
        [Authorize]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken token)
        {
            await _sessionService.CancelAsync(id, CurrentUserId, IsAdmin, token);
            return NoContent();
        }

        [HttpDelete("{id:guid}/teams/{teamId:guid}")]
        [Authorize]
        public async Task<IActionResult> DeleteTeam(Guid id, Guid teamId, CancellationToken token)
        {
            await _sessionService.DeleteTeamAsync(id, teamId, CurrentUserId, IsAdmin, token);
            return NoContent();
        }
    }
}