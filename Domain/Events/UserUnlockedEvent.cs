namespace Domain.Events;

public record UserUnlockedEvent
{
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string UnlockMessage { get; init; } = string.Empty;
}