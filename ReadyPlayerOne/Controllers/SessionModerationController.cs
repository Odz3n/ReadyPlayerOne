
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ReadyPlayerOne.DTOs.GameSessions;
using ReadyPlayerOne.Interfaces;
using System.Security.Claims;

namespace ReadyPlayerOne.Controllers;

[ApiController]
[Authorize]
[Route("api/game-sessions/{sessionId:guid}")]
public sealed class SessionModerationController : ControllerBase
{
    private readonly ISessionModerationService _service;

    public SessionModerationController(ISessionModerationService service)
    {
        _service = service;
    }

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User ID not found in JWT.");

    private bool IsAdmin => User.IsInRole("Admin");

    [HttpDelete("participants/{userId}")]
    public async Task<IActionResult> Kick(
        Guid sessionId,
        string userId,
        CancellationToken ct)
    {
        try
        {
            await _service.KickAsync(sessionId, userId, UserId, IsAdmin, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorDetails(400, ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ErrorDetails(404, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ErrorDetails(409, ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("bans/{userId}")]
    public async Task<IActionResult> Ban(
        Guid sessionId,
        string userId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] BanUserRequest? request,
        CancellationToken ct)
    {
        try
        {
            await _service.BanAsync(sessionId, userId, UserId, IsAdmin, request?.Reason, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorDetails(400, ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ErrorDetails(404, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ErrorDetails(409, ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("bans/{userId}")]
    public async Task<IActionResult> Unban(
        Guid sessionId,
        string userId,
        CancellationToken ct)
    {
        try
        {
            await _service.UnbanAsync(sessionId, userId, UserId, IsAdmin, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorDetails(400, ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ErrorDetails(404, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ErrorDetails(409, ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("bans")]
    public async Task<IActionResult> GetBans(
        Guid sessionId,
        CancellationToken ct)
    {
        try
        {
            var bans = await _service.GetSessionBansAsync(sessionId, UserId, IsAdmin,ct);
            return Ok(bans);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ErrorDetails(400, ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ErrorDetails(404, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ErrorDetails(409, ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private static ProblemDetails ErrorDetails(int status, string message)
    {
        return new ProblemDetails
        {
            Status = status,
            Detail = message
        };
    }
}