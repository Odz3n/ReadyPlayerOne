namespace ReadyPlayerOne.DTOs.Auth
{
    public class AuthSessionResult
    {
        public AuthResponse Access { get; set; } = null!;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
