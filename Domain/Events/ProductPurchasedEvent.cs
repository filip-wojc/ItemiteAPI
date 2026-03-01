namespace Domain.Events;

public record ProductPurchasedEvent
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int SellerId { get; init; }
    public int BuyerId { get; init; }
    public string BuyerUserName { get; init; } = string.Empty;
    public string? ProductPhotoUrl { get; init; }
}
