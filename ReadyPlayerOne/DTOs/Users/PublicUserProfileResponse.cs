namespace ReadyPlayerOne.DTOs.Users
{
    public class PublicUserProfileResponse : PublicUserResponse
    {
        public string? Bio { get; set; }
        public List<PublicUserSessionResponse> ActiveSessions { get; set; } = new();
    }
}
