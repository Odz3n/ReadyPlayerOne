using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.DTOs.Users;

namespace ReadyPlayerOne.Interfaces
{
    public interface IUserService
    {
        Task<PagedResult<PublicUserResponse>> GetAllAsync(UserQueryRequest request, CancellationToken token);
        Task<PublicUserProfileResponse?> GetByIdAsync(string id, CancellationToken token);
    }
}
