using Microsoft.EntityFrameworkCore;
using ReadyPlayerOne.Data;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.GameSessions;
using ReadyPlayerOne.Interfaces;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.Services
{
    public class GameSessionService : IGameSessionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IRepository<GameSession, Guid> _sessionRepository;

        public GameSessionService(ApplicationDbContext context, IRepository<GameSession, Guid> sessionRepository)
        {
            _context = context;
            _sessionRepository = sessionRepository;
        }

        public async Task<PagedResult<GameSessionResponse>> GetAllAsync(GameSessionQueryRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.PageNumber < 1 || request.PageSize is < 1 or > 100)
                throw new ArgumentException("Invalid pagination parameters.", nameof(request));

            if (request.GameId == Guid.Empty)
                throw new ArgumentException("GameId cannot be empty.", nameof(request));

            if (request.Status.HasValue && !Enum.IsDefined(request.Status.Value))
                throw new ArgumentException("Invalid game session status.", nameof(request));

            if (request.Mode.HasValue && !Enum.IsDefined(request.Mode.Value))
                throw new ArgumentException("Invalid session mode.", nameof(request));

            IQueryable<GameSession> query = _sessionRepository.GetAllAsQueryable();

            if (request.GameId.HasValue)
                query = query.Where(s => s.GameId == request.GameId.Value);

            if (request.Status.HasValue)
                query = query.Where(s => s.Status == request.Status.Value);

            if (request.Mode.HasValue)
                query = query.Where(s => s.Mode == request.Mode.Value);

            int totalCount = await query.CountAsync(token);
            long offset = ((long)request.PageNumber - 1) * request.PageSize;

            var sessions = offset > int.MaxValue
                ? new List<GameSessionResponse>()
                : await query
                    .OrderByDescending(s => s.CreatedAt)
                    .ThenBy(s => s.Id)
                    .Skip((int)offset)
                    .Take(request.PageSize)
                    .Select(s => new GameSessionResponse
                    {
                        Id = s.Id,
                        GameId = s.GameId,
                        GameName = s.Game.Name,
                        HostId = s.HostId,
                        Name = s.Name,
                        Mode = s.Mode,
                        Status = s.Status,
                        MaxPlayers = s.MaxPlayers,
                        CurrentPlayers = s.Participants.Count,
                        RequiredTeams = s.RequiredTeams,
                        PlayersPerTeam = s.PlayersPerTeam,
                        CreatedAt = s.CreatedAt,
                        StartedAt = s.StartedAt,
                        EndedAt = s.EndedAt
                    })
                    .ToListAsync(token);

            return new PagedResult<GameSessionResponse>
            {
                Items = sessions,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<GameSessionResponse?> GetByIdAsync(Guid id, CancellationToken token)
        {
            var session = await _sessionRepository.GetByIdAsync(id, token);
            return session is null ? null : MapResponse(session);
        }

        public async Task<GameSessionResponse> CreateAsync(CreateGameSessionRequest request, string userId, CancellationToken token)
        {
            ValidateConfiguration(request.Name, request.Mode, request.MaxPlayers, request.RequiredTeams, request.PlayersPerTeam);

            if (!await _context.Games.AnyAsync(g => g.Id == request.GameId, token))
                throw new KeyNotFoundException("Game not found.");

            var session = new GameSession
            {
                Id = Guid.NewGuid(),
                GameId = request.GameId,
                HostId = userId,
                Name = request.Name.Trim(),
                Mode = request.Mode,
                Status = GameSessionStatus.Waiting,
                MaxPlayers = request.MaxPlayers,
                RequiredTeams = request.Mode == SessionMode.TeamBased ? request.RequiredTeams : null,
                PlayersPerTeam = request.Mode == SessionMode.TeamBased ? request.PlayersPerTeam : null,
                CreatedAt = DateTime.UtcNow
            };

            session.Participants.Add(new SessionParticipant
            {
                Id = Guid.NewGuid(),
                GameSessionId = session.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });

            await _sessionRepository.AddAsync(session, token);
            return (await GetByIdAsync(session.Id, token))!;
        }

        public async Task<bool> UpdateAsync(Guid id, UpdateGameSessionRequest request, string userId, bool isAdmin, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureOwner(session, userId, isAdmin);
            EnsureWaiting(session);
            ValidateConfiguration(request.Name, request.Mode, request.MaxPlayers, request.RequiredTeams, request.PlayersPerTeam);

            if (session.Participants.Count > request.MaxPlayers)
                throw new InvalidOperationException("MaxPlayers cannot be smaller than the current participant count.");

            if (session.Teams.Count > 0 && (session.Mode != request.Mode ||
                session.RequiredTeams != request.RequiredTeams ||
                session.PlayersPerTeam != request.PlayersPerTeam))
                throw new InvalidOperationException("Cannot change team configuration after teams have been created.");

            session.Name = request.Name.Trim();
            session.Mode = request.Mode;
            session.MaxPlayers = request.MaxPlayers;
            session.RequiredTeams = request.Mode == SessionMode.TeamBased ? request.RequiredTeams : null;
            session.PlayersPerTeam = request.Mode == SessionMode.TeamBased ? request.PlayersPerTeam : null;

            await _sessionRepository.UpdateAsync(session, id, token);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id, string userId, bool isAdmin, CancellationToken token)
        {
            var session = await _sessionRepository.GetByIdAsync(id, token);
            if (session is null)
                return false;

            EnsureOwner(session, userId, isAdmin);

            if (session.Status != GameSessionStatus.Waiting && session.Status != GameSessionStatus.Cancelled)
                throw new InvalidOperationException("Only waiting or cancelled sessions can be deleted.");

            await _sessionRepository.DeleteAsync(id, token);
            return true;
        }

        public async Task JoinAsync(Guid id, string userId, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureWaiting(session);

            if (session.Participants.Any(p => p.UserId == userId))
                throw new InvalidOperationException("User is already a participant.");

            if (session.Participants.Count >= session.MaxPlayers)
                throw new InvalidOperationException("Session is full.");

            _context.SessionParticipants.Add(new SessionParticipant
            {
                Id = Guid.NewGuid(),
                GameSessionId = id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(token);
        }

        public async Task LeaveAsync(Guid id, string userId, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureWaiting(session);

            if (session.HostId == userId)
                throw new InvalidOperationException("The host cannot leave their own session.");

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant is null)
                throw new KeyNotFoundException("Participant not found.");

            _context.SessionParticipants.Remove(participant);
            await _context.SaveChangesAsync(token);
        }

        public async Task SetReadyAsync(Guid id, string userId, bool isReady, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureWaiting(session);

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant is null)
                throw new KeyNotFoundException("Participant not found.");

            participant.IsReady = isReady;
            await _context.SaveChangesAsync(token);
        }

        public async Task<TeamResponse> CreateTeamAsync(Guid id, CreateTeamRequest request, string userId, bool isAdmin, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureOwner(session, userId, isAdmin);
            EnsureWaiting(session);
            EnsureTeamBased(session);

            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 50)
                throw new ArgumentException("Team name must contain between 1 and 50 characters.");

            if (session.Teams.Count >= session.RequiredTeams!.Value)
                throw new InvalidOperationException("Required number of teams has already been created.");

            if (session.Teams.Any(t => t.Name.Equals(request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("A team with this name already exists in the session.");

            var team = new Team
            {
                Id = Guid.NewGuid(),
                GameSessionId = id,
                Name = request.Name.Trim()
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync(token);

            return new TeamResponse { Id = team.Id, Name = team.Name, MemberCount = 0 };
        }

        public async Task JoinTeamAsync(Guid id, Guid teamId, string userId, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureWaiting(session);
            EnsureTeamBased(session);

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant is null)
                throw new KeyNotFoundException("Participant not found.");

            var team = session.Teams.FirstOrDefault(t => t.Id == teamId);
            if (team is null)
                throw new KeyNotFoundException("Team not found in this session.");

            if (participant.TeamId == teamId)
                return;

            int memberCount = session.Participants.Count(p => p.TeamId == teamId);
            if (memberCount >= session.PlayersPerTeam!.Value)
                throw new InvalidOperationException("Team is full.");

            participant.TeamId = teamId;
            participant.IsReady = false;
            await _context.SaveChangesAsync(token);
        }

        public async Task LeaveTeamAsync(Guid id, string userId, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureWaiting(session);
            EnsureTeamBased(session);

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant is null)
                throw new KeyNotFoundException("Participant not found.");

            participant.TeamId = null;
            participant.IsReady = false;
            await _context.SaveChangesAsync(token);
        }

        public async Task StartAsync(Guid id, string userId, bool isAdmin, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureOwner(session, userId, isAdmin);
            EnsureWaiting(session);

            if (session.Participants.Count < 2)
                throw new InvalidOperationException("At least two players are required to start.");

            if (session.Participants.Any(p => !p.IsReady))
                throw new InvalidOperationException("All participants must be ready.");

            if (session.Mode == SessionMode.TeamBased)
            {
                if (session.Teams.Count != session.RequiredTeams)
                    throw new InvalidOperationException("Not all required teams have been created.");

                if (session.Participants.Any(p => p.TeamId is null))
                    throw new InvalidOperationException("All participants must belong to a team.");

                if (session.Teams.Any(t => session.Participants.Count(p => p.TeamId == t.Id) != session.PlayersPerTeam))
                    throw new InvalidOperationException("All teams must have the required number of players.");
            }

            session.Status = GameSessionStatus.InProgress;
            session.StartedAt = DateTime.UtcNow;
            await _sessionRepository.UpdateAsync(session, id, token);
        }

        public async Task CompleteAsync(Guid id, string userId, bool isAdmin, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureOwner(session, userId, isAdmin);

            if (session.Status != GameSessionStatus.InProgress)
                throw new InvalidOperationException("Only sessions in progress can be completed.");

            session.Status = GameSessionStatus.Completed;
            session.EndedAt = DateTime.UtcNow;
            await _sessionRepository.UpdateAsync(session, id, token);
        }

        public async Task CancelAsync(Guid id, string userId, bool isAdmin, CancellationToken token)
        {
            var session = await GetRequiredSessionAsync(id, token);
            EnsureOwner(session, userId, isAdmin);
            EnsureWaiting(session);

            session.Status = GameSessionStatus.Cancelled;
            session.EndedAt = DateTime.UtcNow;
            await _sessionRepository.UpdateAsync(session, id, token);
        }

        private async Task<GameSession> GetRequiredSessionAsync(Guid id, CancellationToken token)
        {
            return await _sessionRepository.GetByIdAsync(id, token)
                ?? throw new KeyNotFoundException($"Session with ID '{id}' not found.");
        }

        private static void EnsureOwner(GameSession session, string userId, bool isAdmin)
        {
            if (session.HostId != userId && !isAdmin)
                throw new UnauthorizedAccessException("Only the host or an administrator can perform this action.");
        }

        private static void EnsureWaiting(GameSession session)
        {
            if (session.Status != GameSessionStatus.Waiting)
                throw new InvalidOperationException("Session must be in Waiting status.");
        }

        private static void EnsureTeamBased(GameSession session)
        {
            if (session.Mode != SessionMode.TeamBased)
                throw new InvalidOperationException("This operation is available only in TeamBased mode.");
        }

        private static void ValidateConfiguration(string name, SessionMode mode, int maxPlayers, int? requiredTeams, int? playersPerTeam)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
                throw new ArgumentException("Session name must contain between 1 and 100 characters.");

            if (!Enum.IsDefined(mode))
                throw new ArgumentException("Invalid session mode.");

            if (maxPlayers < 2 || maxPlayers > 100)
                throw new ArgumentException("MaxPlayers must be between 2 and 100.");

            if (mode != SessionMode.TeamBased)
            {
                if (requiredTeams.HasValue || playersPerTeam.HasValue)
                    throw new ArgumentException("Team settings are only allowed in TeamBased mode.");

                return;
            }

            if (requiredTeams is null || requiredTeams < 2 || playersPerTeam is null || playersPerTeam < 1)
                throw new ArgumentException("TeamBased mode requires at least 2 teams and at least 1 player per team.");

            if ((long)requiredTeams.Value * playersPerTeam.Value != maxPlayers)
                throw new ArgumentException("RequiredTeams multiplied by PlayersPerTeam must equal MaxPlayers.");
        }

        private static GameSessionResponse MapResponse(GameSession session)
        {
            return new GameSessionResponse
            {
                Id = session.Id,
                GameId = session.GameId,
                GameName = session.Game.Name,
                HostId = session.HostId,
                Name = session.Name,
                Mode = session.Mode,
                Status = session.Status,
                MaxPlayers = session.MaxPlayers,
                CurrentPlayers = session.Participants.Count,
                RequiredTeams = session.RequiredTeams,
                PlayersPerTeam = session.PlayersPerTeam,
                CreatedAt = session.CreatedAt,
                StartedAt = session.StartedAt,
                EndedAt = session.EndedAt,
                Teams = session.Teams.Select(t => new TeamResponse
                {
                    Id = t.Id,
                    Name = t.Name,
                    MemberCount = session.Participants.Count(p => p.TeamId == t.Id)
                }).ToList(),
                Participants = session.Participants.Select(p => new SessionParticipantResponse
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    UserName = p.User.UserName ?? string.Empty,
                    TeamId = p.TeamId,
                    IsReady = p.IsReady,
                    JoinedAt = p.JoinedAt
                }).ToList()
            };
        }

        public async Task DeleteTeamAsync(Guid sessionId, Guid teamId, string userId, bool isAdmin, CancellationToken token)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(token);

            var session = await GetRequiredSessionAsync(sessionId, token);
            EnsureOwner(session, userId, isAdmin);
            EnsureWaiting(session);
            EnsureTeamBased(session);

            var team = session.Teams.FirstOrDefault(t => t.Id == teamId);
            if (team is null)
                throw new KeyNotFoundException($"Team with ID '{teamId}' not found in this session.");

            var members = session.Participants.Where(p => p.TeamId == teamId).ToList();

            foreach (var member in members)
            {
                member.TeamId = null;
                member.IsReady = false;
            }

            await _context.SaveChangesAsync(token);

            _context.Teams.Remove(team);
            await _context.SaveChangesAsync(token);

            await transaction.CommitAsync(token);
        }
    }
}