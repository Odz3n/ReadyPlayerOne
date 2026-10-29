using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
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

		public async Task<PagedResult<GameSessionListResponse>> GetAllAsync(GameSessionQueryRequest request, CancellationToken token)
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
				? new List<GameSessionListResponse>()
				: await query
					.OrderByDescending(s => s.CreatedAt)
					.ThenBy(s => s.Id)
					.Skip((int)offset)
					.Take(request.PageSize)
					.Select(s => new GameSessionListResponse
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

			return new PagedResult<GameSessionListResponse>
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
			ArgumentNullException.ThrowIfNull(request);
			ValidateConfiguration(request.Name, request.Mode, request.MaxPlayers, request.RequiredTeams, request.PlayersPerTeam);
			if (string.IsNullOrWhiteSpace(userId))
				throw new UnauthorizedAccessException("User ID not found in token.");

			Guid id = Guid.NewGuid();
			Guid participantId = Guid.NewGuid();
			DateTime now = DateTime.UtcNow;
			var strategy = _context.Database.CreateExecutionStrategy();

			await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);

				if (await _context.GameSessions.AsNoTracking().AnyAsync(s => s.Id == id, token))
				{
					await transaction.CommitAsync(token);
					return;
				}

				if (!await _context.Users.AsNoTracking().AnyAsync(u => u.Id == userId, token))
					throw new KeyNotFoundException("User not found.");

				if (!await _context.Games.AsNoTracking().AnyAsync(g => g.Id == request.GameId, token))
					throw new KeyNotFoundException("Game not found.");

				int mode = (int)request.Mode;
				int status = (int)GameSessionStatus.Waiting;
				int? requiredTeams = request.Mode == SessionMode.TeamBased ? request.RequiredTeams : null;
				int? playersPerTeam = request.Mode == SessionMode.TeamBased ? request.PlayersPerTeam : null;
				string name = request.Name.Trim();

				await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [GameSessions]
                        ([Id], [GameId], [HostId], [Name], [Mode], [Status],
                         [MaxPlayers], [RequiredTeams], [PlayersPerTeam], [CreatedAt],
                         [StartedAt], [EndedAt])
                    VALUES ({id}, {request.GameId}, {userId}, {name}, {mode}, {status},
                            {request.MaxPlayers}, {requiredTeams}, {playersPerTeam}, {now},
                            NULL, NULL);
                    """, token);

				await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [SessionParticipants]
                        ([Id], [GameSessionId], [UserId], [TeamId], [JoinedAt], [IsReady])
                    VALUES ({participantId}, {id}, {userId}, NULL, {now}, CAST(0 AS bit));
                    """, token);

				await transaction.CommitAsync(token);
			});

			return (await GetByIdAsync(id, token))!;
		}

		public async Task<bool> UpdateAsync(Guid id, UpdateGameSessionRequest request, string userId, bool isAdmin, CancellationToken token)
		{
			ArgumentNullException.ThrowIfNull(request);
			ValidateConfiguration(request.Name, request.Mode, request.MaxPlayers, request.RequiredTeams, request.PlayersPerTeam);
			var strategy = _context.Database.CreateExecutionStrategy();

			return await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
				var session = await GetSessionSnapshotAsync(id, token);
				if (session is null)
					return false;

				EnsureOwner(session, userId, isAdmin);
				EnsureWaiting(session);

				int participantCount = await _context.SessionParticipants.AsNoTracking()
					.CountAsync(p => p.GameSessionId == id, token);
				if (participantCount > request.MaxPlayers)
					throw new InvalidOperationException("MaxPlayers cannot be smaller than the current participant count.");

				bool hasTeams = await _context.Teams.AsNoTracking()
					.AnyAsync(t => t.GameSessionId == id, token);
				if (hasTeams && (session.Mode != request.Mode ||
					session.RequiredTeams != request.RequiredTeams ||
					session.PlayersPerTeam != request.PlayersPerTeam))
					throw new InvalidOperationException("Cannot change team configuration after teams have been created.");

				int changed = await _context.GameSessions
					.Where(s => s.Id == id && s.Status == GameSessionStatus.Waiting)
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(s => s.Name, request.Name.Trim())
						.SetProperty(s => s.Mode, request.Mode)
						.SetProperty(s => s.MaxPlayers, request.MaxPlayers)
						.SetProperty(s => s.RequiredTeams, request.Mode == SessionMode.TeamBased ? request.RequiredTeams : null)
						.SetProperty(s => s.PlayersPerTeam, request.Mode == SessionMode.TeamBased ? request.PlayersPerTeam : null), token);

				if (changed != 1)
					throw new InvalidOperationException("Session configuration could not be updated.");

				await transaction.CommitAsync(token);
				return true;
			});
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

		private Task<GameSession?> GetSessionSnapshotAsync(Guid id, CancellationToken token)
			=> _context.GameSessions
				.AsNoTracking()
				.SingleOrDefaultAsync(s => s.Id == id, token);

		public async Task JoinAsync(Guid id, string userId, CancellationToken token)
		{
			if (id == Guid.Empty)
				throw new ArgumentException("Session ID cannot be empty.", nameof(id));

			if (string.IsNullOrWhiteSpace(userId))
				throw new UnauthorizedAccessException("User ID not found in token.");

			var participantId = Guid.NewGuid();
			var joinedAt = DateTime.UtcNow;
			var strategy = _context.Database.CreateExecutionStrategy();

			try
			{
				await strategy.ExecuteAsync(async () =>
				{
					await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);

					var session = await GetSessionSnapshotAsync(id, token)
						?? throw new KeyNotFoundException($"Session with ID '{id}' not found.");

					var existingId = await _context.SessionParticipants.AsNoTracking()
						.Where(p => p.GameSessionId == id && p.UserId == userId)
						.Select(p => (Guid?)p.Id)
						.FirstOrDefaultAsync(token);

					if (existingId.HasValue)
					{
						if (existingId.Value != participantId)
							throw new InvalidOperationException("User is already a participant.");

						await transaction.CommitAsync(token);
						return;
					}

					if (await _context.SessionBans.AsNoTracking()
						.AnyAsync(b => b.GameSessionId == id && b.UserId == userId, token))
						throw new SessionAccessDeniedException("You are banned from this session.");

					if (session.Status != GameSessionStatus.Waiting)
						throw new InvalidOperationException("Session must be in Waiting status.");

					var participantCount = await _context.SessionParticipants.AsNoTracking()
						.CountAsync(p => p.GameSessionId == id, token);

					if (participantCount >= session.MaxPlayers)
						throw new InvalidOperationException("Session is full.");

					await _context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO [SessionParticipants]
                            ([Id], [GameSessionId], [UserId], [TeamId], [JoinedAt], [IsReady])
                        VALUES ({participantId}, {id}, {userId}, NULL, {joinedAt}, CAST(0 AS bit));
                        """, token);

					await transaction.CommitAsync(token);
				});
			}
			catch (SqlException ex) when (ex.Number is 2601 or 2627)
			{
				throw new InvalidOperationException("User is already a participant.", ex);
			}
		}

		public async Task LeaveAsync(Guid id, string userId, CancellationToken token)
		{
			if (id == Guid.Empty)
				throw new ArgumentException("Session ID cannot be empty.", nameof(id));

			if (string.IsNullOrWhiteSpace(userId))
				throw new UnauthorizedAccessException("User ID not found in token.");

			Guid? removedParticipantId = null;
			var strategy = _context.Database.CreateExecutionStrategy();

			await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);

				var session = await GetSessionSnapshotAsync(id, token)
					?? throw new KeyNotFoundException($"Session with ID '{id}' not found.");

				var participantId = await _context.SessionParticipants.AsNoTracking()
					.Where(p => p.GameSessionId == id && p.UserId == userId)
					.Select(p => (Guid?)p.Id)
					.FirstOrDefaultAsync(token);

				if (removedParticipantId.HasValue && !participantId.HasValue)
				{
					await transaction.CommitAsync(token);
					return;
				}

				if (session.Status != GameSessionStatus.Waiting)
					throw new InvalidOperationException("Session must be in Waiting status.");

				if (session.HostId == userId)
					throw new InvalidOperationException("The host cannot leave their own session.");

				if (!participantId.HasValue)
					throw new KeyNotFoundException("Participant not found.");

				if (removedParticipantId.HasValue && participantId.Value != removedParticipantId.Value)
					throw new InvalidOperationException("Membership changed concurrently. Please retry.");

				removedParticipantId = participantId.Value;

				await _context.SessionParticipants
					.Where(p => p.Id == participantId.Value && p.GameSessionId == id)
					.ExecuteDeleteAsync(token);

				await transaction.CommitAsync(token);
			});
		}

		public async Task SetReadyAsync(Guid id, string userId, bool isReady, CancellationToken token)
		{
			var strategy = _context.Database.CreateExecutionStrategy();
			await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
				var session = await GetSessionSnapshotAsync(id, token)
					?? throw new KeyNotFoundException($"Session with ID '{id}' not found.");
				EnsureWaiting(session);

				int changed = await _context.SessionParticipants
					.Where(p => p.GameSessionId == id && p.UserId == userId)
					.ExecuteUpdateAsync(setters => setters.SetProperty(p => p.IsReady, isReady), token);
				if (changed != 1)
					throw new KeyNotFoundException("Participant not found.");

				await transaction.CommitAsync(token);
			});
		}

		public async Task<TeamResponse> CreateTeamAsync(Guid id, CreateTeamRequest request, string userId, bool isAdmin, CancellationToken token)
		{
			ArgumentNullException.ThrowIfNull(request);
			if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 50)
				throw new ArgumentException("Team name must contain between 1 and 50 characters.");

			string name = request.Name.Trim();
			Guid teamId = Guid.NewGuid();
			var strategy = _context.Database.CreateExecutionStrategy();

			try
			{
				return await strategy.ExecuteAsync(async () =>
				{
					await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
					var session = await GetSessionSnapshotAsync(id, token)
						?? throw new KeyNotFoundException($"Session with ID '{id}' not found.");
					EnsureOwner(session, userId, isAdmin);
					EnsureWaiting(session);
					EnsureTeamBased(session);

					bool alreadyInserted = await _context.Teams.AsNoTracking()
						.AnyAsync(t => t.GameSessionId == id && t.Id == teamId, token);
					if (alreadyInserted)
					{
						await transaction.CommitAsync(token);
						return BuildNewTeamResponse(teamId, name, session.PlayersPerTeam!.Value);
					}

					int teamCount = await _context.Teams.AsNoTracking()
						.CountAsync(t => t.GameSessionId == id, token);
					if (teamCount >= session.RequiredTeams!.Value)
						throw new InvalidOperationException("Required number of teams has already been created.");

					bool duplicateName = await _context.Teams.AsNoTracking()
						.AnyAsync(t => t.GameSessionId == id &&
							EF.Functions.Collate(t.Name, "Latin1_General_100_CI_AS") == name, token);
					if (duplicateName)
						throw new InvalidOperationException("A team with this name already exists in the session.");

					await _context.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO [Teams] ([Id], [GameSessionId], [Name])
                        VALUES ({teamId}, {id}, {name});
                        """, token);

					await transaction.CommitAsync(token);
					return BuildNewTeamResponse(teamId, name, session.PlayersPerTeam!.Value);
				});
			}
			catch (SqlException ex) when (ex.Number is 2601 or 2627)
			{
				throw new InvalidOperationException("A team with this name already exists in the session.", ex);
			}
		}

		private static TeamResponse BuildNewTeamResponse(Guid teamId, string name, int capacity)
			=> new()
			{
				Id = teamId,
				Name = name,
				MemberCount = 0,
				MaxMembers = capacity,
				AvailableSlots = capacity
			};

		public async Task JoinTeamAsync(Guid id, Guid teamId, string userId, CancellationToken token)
		{
			if (teamId == Guid.Empty)
				throw new ArgumentException("Team ID cannot be empty.", nameof(teamId));

			var strategy = _context.Database.CreateExecutionStrategy();
			await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
				var session = await GetSessionSnapshotAsync(id, token)
					?? throw new KeyNotFoundException($"Session with ID '{id}' not found.");
				EnsureWaiting(session);
				EnsureTeamBased(session);

				var participant = await _context.SessionParticipants.AsNoTracking()
					.Where(p => p.GameSessionId == id && p.UserId == userId)
					.Select(p => new { p.Id, p.TeamId })
					.SingleOrDefaultAsync(token);
				if (participant is null)
					throw new KeyNotFoundException("Join the session before joining a team.");

				bool teamExists = await _context.Teams.AsNoTracking()
					.AnyAsync(t => t.Id == teamId && t.GameSessionId == id, token);
				if (!teamExists)
					throw new KeyNotFoundException("Team not found in this session.");

				if (participant.TeamId == teamId)
				{
					await transaction.CommitAsync(token);
					return;
				}

				int memberCount = await _context.SessionParticipants.AsNoTracking()
					.CountAsync(p => p.GameSessionId == id && p.TeamId == teamId, token);
				if (memberCount >= session.PlayersPerTeam!.Value)
					throw new InvalidOperationException("Team is full.");

				int changed = await _context.SessionParticipants
					.Where(p => p.Id == participant.Id && p.GameSessionId == id)
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(p => p.TeamId, (Guid?)teamId)
						.SetProperty(p => p.IsReady, false), token);
				if (changed != 1)
					throw new InvalidOperationException("Participant membership changed. Please retry.");

				await transaction.CommitAsync(token);
			});
		}

		public async Task LeaveTeamAsync(Guid id, string userId, CancellationToken token)
		{
			var strategy = _context.Database.CreateExecutionStrategy();
			await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
				var session = await GetSessionSnapshotAsync(id, token)
					?? throw new KeyNotFoundException($"Session with ID '{id}' not found.");
				EnsureWaiting(session);
				EnsureTeamBased(session);

				int changed = await _context.SessionParticipants
					.Where(p => p.GameSessionId == id && p.UserId == userId)
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(p => p.TeamId, (Guid?)null)
						.SetProperty(p => p.IsReady, false), token);
				if (changed != 1)
					throw new KeyNotFoundException("Participant not found.");

				await transaction.CommitAsync(token);
			});
		}

		public async Task StartAsync(Guid id, string userId, bool isAdmin, CancellationToken token)
		{
			var strategy = _context.Database.CreateExecutionStrategy();
			await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
				var session = await GetSessionSnapshotAsync(id, token)
					?? throw new KeyNotFoundException($"Session with ID '{id}' not found.");
				EnsureOwner(session, userId, isAdmin);
				EnsureWaiting(session);

				var participants = await _context.SessionParticipants.AsNoTracking()
					.Where(p => p.GameSessionId == id)
					.Select(p => new { p.TeamId, p.IsReady })
					.ToListAsync(token);

				if (participants.Count < 2)
					throw new InvalidOperationException("At least two players are required to start.");
				if (participants.Any(p => !p.IsReady))
					throw new InvalidOperationException("All participants must be ready.");

				if (session.Mode == SessionMode.TeamBased)
				{
					var teamIds = await _context.Teams.AsNoTracking()
						.Where(t => t.GameSessionId == id)
						.Select(t => t.Id)
						.ToListAsync(token);

					if (teamIds.Count != session.RequiredTeams)
						throw new InvalidOperationException("Not all required teams have been created.");
					if (participants.Any(p => p.TeamId is null))
						throw new InvalidOperationException("All participants must belong to a team.");
					if (teamIds.Any(tid => participants.Count(p => p.TeamId == tid) != session.PlayersPerTeam))
						throw new InvalidOperationException("All teams must have the required number of players.");
				}

				int changed = await _context.GameSessions
					.Where(s => s.Id == id && s.Status == GameSessionStatus.Waiting)
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(s => s.Status, GameSessionStatus.InProgress)
						.SetProperty(s => s.StartedAt, (DateTime?)DateTime.UtcNow), token);
				if (changed != 1)
					throw new InvalidOperationException("Session is no longer waiting.");

				await transaction.CommitAsync(token);
			});
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
			var strategy = _context.Database.CreateExecutionStrategy();
			await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
				var session = await GetSessionSnapshotAsync(id, token)
					?? throw new KeyNotFoundException($"Session with ID '{id}' not found.");
				EnsureOwner(session, userId, isAdmin);
				EnsureWaiting(session);

				int changed = await _context.GameSessions
					.Where(s => s.Id == id && s.Status == GameSessionStatus.Waiting)
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(s => s.Status, GameSessionStatus.Cancelled)
						.SetProperty(s => s.EndedAt, (DateTime?)DateTime.UtcNow), token);
				if (changed != 1)
					throw new InvalidOperationException("Session is no longer waiting.");

				await transaction.CommitAsync(token);
			});
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
					MemberCount = session.Participants.Count(p => p.TeamId == t.Id),
					MaxMembers = session.PlayersPerTeam ?? 0,
					AvailableSlots = Math.Max(0, (session.PlayersPerTeam ?? 0) - session.Participants.Count(p => p.TeamId == t.Id))
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
			if (teamId == Guid.Empty)
				throw new ArgumentException("Team ID cannot be empty.", nameof(teamId));

			bool deletionAttempted = false;
			var strategy = _context.Database.CreateExecutionStrategy();
			await strategy.ExecuteAsync(async () =>
			{
				await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
				var session = await GetSessionSnapshotAsync(sessionId, token)
					?? throw new KeyNotFoundException($"Session with ID '{sessionId}' not found.");
				EnsureOwner(session, userId, isAdmin);
				EnsureWaiting(session);
				EnsureTeamBased(session);

				bool teamExists = await _context.Teams.AsNoTracking()
					.AnyAsync(t => t.Id == teamId && t.GameSessionId == sessionId, token);
				if (!teamExists)
				{
					if (!deletionAttempted)
						throw new KeyNotFoundException("Team not found in this session.");

					await transaction.CommitAsync(token);
					return;
				}

				deletionAttempted = true;
				await _context.SessionParticipants
					.Where(p => p.GameSessionId == sessionId && p.TeamId == teamId)
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(p => p.TeamId, (Guid?)null)
						.SetProperty(p => p.IsReady, false), token);

				await _context.Teams
					.Where(t => t.Id == teamId && t.GameSessionId == sessionId)
					.ExecuteDeleteAsync(token);

				await transaction.CommitAsync(token);
			});
		}
	}
}