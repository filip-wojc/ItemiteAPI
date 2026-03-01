using Domain.DTOs.Notifications;
using Domain.Enums;
using Domain.Events;
using Infrastructure.Interfaces.Services;
using MassTransit;

namespace Infrastructure.Consumers;

public class BidPlacedConsumer(INotificationService notificationService) : IConsumer<BidPlacedEvent>
{
    public async Task Consume(ConsumeContext<BidPlacedEvent> context)
    {
        var evt = context.Message;

        await notificationService.SendNotification([evt.AuctionOwnerId], evt.BidderId, new NotificationInfo
        {
            Message = $"User {evt.BidderUserName} placed a new bid with value: {evt.BidPrice} in your auction {evt.AuctionName}",
            ListingId = evt.AuctionId,
            ResourceType = ResourceType.Auction.ToString(),
            NotificationImageUrl = evt.AuctionPhotoUrl
        });

        if (evt.FormerHighestBidderId.HasValue)
        {
            await notificationService.SendNotification([evt.FormerHighestBidderId.Value], evt.BidderId, new NotificationInfo
            {
                Message = $"User {evt.BidderUserName} placed a new bid with value: {evt.BidPrice} in auction {evt.AuctionName}. You are no longer the highest bidder.",
                ListingId = evt.AuctionId,
                ResourceType = ResourceType.Auction.ToString(),
                NotificationImageUrl = evt.AuctionPhotoUrl
            });
        }
    }
}
