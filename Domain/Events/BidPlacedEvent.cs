namespace Domain.Events;

public record BidPlacedEvent
{
    public int AuctionId { get; init; }
    public string AuctionName { get; init; } = string.Empty;
    public int AuctionOwnerId { get; init; }
    public int BidderId { get; init; }
    public string BidderUserName { get; init; } = string.Empty;
    public decimal BidPrice { get; init; }
    public string? AuctionPhotoUrl { get; init; }
    public int? FormerHighestBidderId { get; init; }
}
