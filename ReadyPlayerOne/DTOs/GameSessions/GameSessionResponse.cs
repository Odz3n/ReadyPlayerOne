using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.DTOs.GameSessions;

public class GameSessionResponse : GameSessionListResponse
{
    public List<TeamResponse> Teams { get; set; } = new();
    public List<SessionParticipantResponse> Participants { get; set; } = new();
}

public class TeamResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public int MemberCount { get; set; }
    public int MaxMembers { get; set; }
    public int AvailableSlots { get; set; }
}

public class SessionParticipantResponse
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public Guid? TeamId { get; set; }
    public bool IsReady { get; set; }
    public DateTime JoinedAt { get; set; }
}