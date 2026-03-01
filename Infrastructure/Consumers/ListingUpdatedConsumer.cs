using Domain.DTOs.Notifications;
using Domain.Events;
using Infrastructure.Interfaces.Services;
using MassTransit;

namespace Infrastructure.Consumers;

public class ListingUpdatedConsumer(INotificationService notificationService) : IConsumer<ListingUpdatedEvent>
{
    public async Task Consume(ConsumeContext<ListingUpdatedEvent> context)
    {
        var evt = context.Message;

        if (!evt.FollowerIds.Any())
            return;

        await notificationService.SendNotification(evt.FollowerIds, evt.OwnerId, new NotificationInfo
        {
            Message = $"{evt.ListingName} has been updated.",
            ListingId = evt.ListingId,
            ResourceType = evt.ResourceType,
            NotificationImageUrl = evt.ListingPhotoUrl
        });
    }
}
