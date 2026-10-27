using System.ComponentModel.DataAnnotations;
using ReadyPlayerOne.DTOs.Common;

namespace ReadyPlayerOne.DTOs.Games;

public class GameQueryRequest : PaginationRequest
{
    [MaxLength(100)]
    public string? Search { get; set; }

    public Guid? GenreId { get; set; }
}
