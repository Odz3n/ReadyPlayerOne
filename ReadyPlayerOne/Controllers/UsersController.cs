using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Users;
using ReadyPlayerOne.Interfaces;

namespace ReadyPlayerOne.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        public UsersController(
            IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<PagedResult<PublicUserResponse>>> GetAllAsync(
            [FromQuery] UserQueryRequest request,
            CancellationToken token)
        {
            var result = await _userService.GetAllAsync(request, token);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicUserProfileResponse>> GetByIdAsync(
            string id,
            CancellationToken token)
        {
            var user = await _userService.GetByIdAsync(id, token);
            return user != null ? Ok(user) : NotFound();
        }
    }
}
