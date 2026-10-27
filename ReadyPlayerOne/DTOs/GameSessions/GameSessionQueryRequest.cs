using System.ComponentModel.DataAnnotations;
using ReadyPlayerOne.DTOs.Common;
using ReadyPlayerOne.Models;

namespace ReadyPlayerOne.DTOs.GameSessions;

public class GameSessionQueryRequest : PaginationRequest, IValidatableObject
{
    public Guid? GameId { get; set; }
    public GameSessionStatus? Status { get; set; }
    public SessionMode? Mode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (GameId == Guid.Empty)
            yield return new ValidationResult("GameId cannot be empty.", [nameof(GameId)]);

        if (Status.HasValue && !Enum.IsDefined(Status.Value))
            yield return new ValidationResult("Invalid game session status.", [nameof(Status)]);

        if (Mode.HasValue && !Enum.IsDefined(Mode.Value))
            yield return new ValidationResult("Invalid session mode.", [nameof(Mode)]);
    }
}