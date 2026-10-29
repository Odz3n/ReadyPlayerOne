using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Users;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        public UserService(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResult<PublicUserResponse>> GetAllAsync(UserQueryRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.PageNumber < 1 || request.PageSize is < 1 or > 100)
                throw new ArgumentException("Invalid pagination parameters.", nameof(request));

            if (request.Search?.Length > 100)
                throw new ArgumentException("Search cannot exceed 100 characters.", nameof(request));

            var query = _context.Users.AsNoTracking();
            var search = request.Search?.Trim();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    (u.UserName != null && u.UserName.Contains(search)) ||
                    u.DisplayName.Contains(search));

            int totalCount = await query.CountAsync(token);
            long offset = ((long)request.PageNumber - 1) * request.PageSize;

            var users = offset > int.MaxValue
            ? new List<PublicUserResponse>()
            : await query
                .OrderBy(u => u.UserName)
                .ThenBy(u => u.Id)
                .Skip((int)offset)
                .Take(request.PageSize)
                .Select(u => new PublicUserResponse
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl
                })
                .ToListAsync(token);

            return new PagedResult<PublicUserResponse>
            {
                Items = users,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<PublicUserProfileResponse?> GetByIdAsync(string id, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var user = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == id)
                .Select(u => new PublicUserProfileResponse
                {
                    Id = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    Bio = u.Bio
                }).FirstOrDefaultAsync(token);

            if (user == null)
                return null;

            user.ActiveSessions = await _context.SessionParticipants
                .AsNoTracking()
                .Where(p => p.UserId == id &&
                    (p.GameSession.Status == GameSessionStatus.Waiting ||
                     p.GameSession.Status == GameSessionStatus.InProgress))
                .OrderByDescending(p => p.GameSession.CreatedAt)
                .ThenBy(p => p.GameSessionId)
                .Select(p => new PublicUserSessionResponse
                {
                    Id = p.GameSession.Id,
                    GameId = p.GameSession.GameId,
                    GameName = p.GameSession.Game.Name,
                    Name = p.GameSession.Name,
                    Status = p.GameSession.Status,
                    Mode = p.GameSession.Mode
                })
                .ToListAsync(token);

            return user;
        }
    }
}
