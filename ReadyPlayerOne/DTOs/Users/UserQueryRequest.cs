using ReadyPlayerOne.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace ReadyPlayerOne.DTOs.Users
{
    public class UserQueryRequest: PaginationRequest
    {
        [MaxLength(100)]
        public string? Search { get; set; }
    }
}
