using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReadyPlayerOne.DTOs.Auth;
using ReadyPlayerOne.Interfaces;

namespace ReadyPlayerOne.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(
            IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Registers a new player.
        /// </summary>
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(
            RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);
            if (!result.Succeeded)
                return BadRequest(new
                {
                    Errors = result.Errors.Select(e => e.Description)
                });
            return StatusCode(StatusCodes.Status201Created);
        }

        /// <summary>
        /// Authenticates a player and returns a JWT.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login(
            LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);
            if (result == null)
                return Unauthorized(new
                {
                    Message = "Invalid credentials."
                });
            return Ok(result);
        }
    }
}
