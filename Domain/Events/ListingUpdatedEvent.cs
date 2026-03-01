namespace Domain.Events;

public record ListingUpdatedEvent
{
    public int ListingId { get; init; }
    public string ListingName { get; init; } = string.Empty;
    public int OwnerId { get; init; }
    public string ResourceType { get; init; } = string.Empty;
    public string? ListingPhotoUrl { get; init; }
    public List<int> FollowerIds { get; init; } = [];
}
