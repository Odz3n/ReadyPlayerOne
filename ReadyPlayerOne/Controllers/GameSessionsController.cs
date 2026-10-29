using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.GameSessions;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Services;
using System.Security.Claims;

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

		private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
			?? User.FindFirstValue("sub")
			?? throw new UnauthorizedAccessException("User ID not found in token.");

		private bool IsAdmin => User.IsInRole("Admin");

		[HttpGet]
		[AllowAnonymous]
		public async Task<ActionResult<PagedResult<GameSessionListResponse>>> GetAll(
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
			try
			{
				var session = await _sessionService.CreateAsync(request, CurrentUserId, token);
				return CreatedAtRoute("GetGameSessionById", new { id = session.Id }, session);
			}
			catch (SessionAccessDeniedException ex)
			{
				return StatusCode(403, new ProblemDetails { Status = 403, Title = "Session access denied", Detail = ex.Message });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new ProblemDetails { Status = 400, Detail = ex.Message });
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new ProblemDetails { Status = 404, Detail = ex.Message });
			}
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
			try
			{
				await _sessionService.JoinAsync(id, CurrentUserId, token);
				return NoContent();
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new ProblemDetails { Status = 404, Title = "Session not found", Detail = ex.Message });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid request", Detail = ex.Message });
			}
			catch (SessionAccessDeniedException ex)
			{
				return StatusCode(403, new ProblemDetails { Status = 403, Title = "Session access denied", Detail = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new ProblemDetails { Status = 409, Title = "Cannot join session", Detail = ex.Message });
			}
			catch (UnauthorizedAccessException)
			{
				return Forbid();
			}
		}

		[HttpPost("{id:guid}/leave")]
		[Authorize]
		public async Task<IActionResult> Leave(Guid id, CancellationToken token)
		{
			try
			{
				await _sessionService.LeaveAsync(id, CurrentUserId, token);
				return NoContent();
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new ProblemDetails { Status = 404, Title = "Session or participant not found", Detail = ex.Message });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid request", Detail = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new ProblemDetails { Status = 409, Title = "Cannot leave session", Detail = ex.Message });
			}
			catch (UnauthorizedAccessException)
			{
				return Forbid();
			}
		}

		[HttpPut("{id:guid}/ready")]
		[Authorize]
		public Task<IActionResult> SetReady(Guid id, [FromBody] bool isReady, CancellationToken token)
			=> ExecuteSessionActionAsync(
				() => _sessionService.SetReadyAsync(id, CurrentUserId, isReady, token),
				"Cannot change readiness");

		[HttpPost("{id:guid}/teams")]
		[Authorize]
		public async Task<ActionResult<TeamResponse>> CreateTeam(
			Guid id, [FromBody] CreateTeamRequest request, CancellationToken token)
		{
			try
			{
				var team = await _sessionService.CreateTeamAsync(id, request, CurrentUserId, IsAdmin, token);
				return Ok(team);
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid team data", Detail = ex.Message });
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new ProblemDetails { Status = 404, Title = "Session not found", Detail = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new ProblemDetails { Status = 409, Title = "Cannot create team", Detail = ex.Message });
			}
			catch (UnauthorizedAccessException)
			{
				return Forbid();
			}
		}

		[HttpPost("{id:guid}/teams/{teamId:guid}/join")]
		[Authorize]
		public async Task<IActionResult> JoinTeam(Guid id, Guid teamId, CancellationToken token)
		{
			try
			{
				await _sessionService.JoinTeamAsync(id, teamId, CurrentUserId, token);
				return NoContent();
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid team ID", Detail = ex.Message });
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new ProblemDetails { Status = 404, Title = "Session, team or participant not found", Detail = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new ProblemDetails { Status = 409, Title = "Cannot join team", Detail = ex.Message });
			}
		}

		[HttpPost("{id:guid}/teams/leave")]
		[Authorize]
		public async Task<IActionResult> LeaveTeam(Guid id, CancellationToken token)
		{
			try
			{
				await _sessionService.LeaveTeamAsync(id, CurrentUserId, token);
				return NoContent();
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new ProblemDetails { Status = 404, Title = "Session or participant not found", Detail = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new ProblemDetails { Status = 409, Title = "Cannot leave team", Detail = ex.Message });
			}
		}



		[HttpPost("{id:guid}/start")]
		[Authorize]
		public Task<IActionResult> Start(Guid id, CancellationToken token)
			=> ExecuteSessionActionAsync(
				() => _sessionService.StartAsync(id, CurrentUserId, IsAdmin, token),
				"Cannot start session");

		[HttpPost("{id:guid}/complete")]
		[Authorize]
		public Task<IActionResult> Complete(Guid id, CancellationToken token)
			=> ExecuteSessionActionAsync(
				() => _sessionService.CompleteAsync(id, CurrentUserId, IsAdmin, token),
				"Cannot complete session");

		[HttpPost("{id:guid}/cancel")]
		[Authorize]
		public Task<IActionResult> Cancel(Guid id, CancellationToken token)
			=> ExecuteSessionActionAsync(
				() => _sessionService.CancelAsync(id, CurrentUserId, IsAdmin, token),
				"Cannot cancel session");

		private async Task<IActionResult> ExecuteSessionActionAsync(Func<Task> action, string title)
		{
			try
			{
				await action();
				return NoContent();
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new ProblemDetails { Status = 404, Title = "Resource not found", Detail = ex.Message });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid request", Detail = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new ProblemDetails { Status = 409, Title = title, Detail = ex.Message });
			}
			catch (UnauthorizedAccessException)
			{
				return Forbid();
			}
		}

		[HttpDelete("{id:guid}/teams/{teamId:guid}")]
		[Authorize]
		public async Task<IActionResult> DeleteTeam(Guid id, Guid teamId, CancellationToken token)
		{
			try
			{
				await _sessionService.DeleteTeamAsync(id, teamId, CurrentUserId, IsAdmin, token);
				return NoContent();
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid team ID", Detail = ex.Message });
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new ProblemDetails { Status = 404, Title = "Session or team not found", Detail = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return Conflict(new ProblemDetails { Status = 409, Title = "Cannot delete team", Detail = ex.Message });
			}
			catch (UnauthorizedAccessException)
			{
				return Forbid();
			}
		}
	}
}