using Domain.DTOs.Notifications;
using Domain.Enums;
using Domain.Events;
using Infrastructure.Interfaces.Services;
using MassTransit;

namespace Infrastructure.Consumers;

public class ProductPurchasedConsumer(INotificationService notificationService) : IConsumer<ProductPurchasedEvent>
{
    public async Task Consume(ConsumeContext<ProductPurchasedEvent> context)
    {
        var evt = context.Message;

        await notificationService.SendNotification([evt.SellerId], evt.BuyerId, new NotificationInfo
        {
            Message = $"User {evt.BuyerUserName} has bought your product {evt.ProductName}",
            ListingId = evt.ProductId,
            ResourceType = ResourceType.Product.ToString(),
            NotificationImageUrl = evt.ProductPhotoUrl
        });
    }
}
