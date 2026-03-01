namespace Domain.Events;

public record UserLockedEvent
{
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string LockDate { get; init; } = string.Empty;
    public string LockMessage { get; init; } = string.Empty;
}