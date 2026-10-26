using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReadyPlayerOne.DTOs.Auth;
using ReadyPlayerOne.Interfaces;
using System.IdentityModel.Tokens.Jwt;

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
            SetRefreshTokenCookie(result.RefreshToken);
            return Ok(result.Access);
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
                return Unauthorized();

            var result = await _authService.RefreshAsync(refreshToken);
            if (result == null)
            {
                DeleteRefreshTokenCookie();
                return Unauthorized();
            }

            SetRefreshTokenCookie(result.RefreshToken);
            return Ok(result.Access);
        }
        
        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
                await _authService.LogoutAsync(refreshToken);

            DeleteRefreshTokenCookie();
            return NoContent();
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me()
        {
            return Ok(new
            {
                UserId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                UserName = User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
            });
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
                Path = "/api/auth"
            });
        }

        private void DeleteRefreshTokenCookie()
        {
            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth"
            });
        }
    }
}
