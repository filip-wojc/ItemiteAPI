namespace Domain.Events;

public record UserRegisteredEvent
{
    public string Email { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string EmailToken { get; init; } = string.Empty;
}