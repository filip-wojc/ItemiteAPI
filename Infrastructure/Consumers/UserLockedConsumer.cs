using Domain.Events;
using Infrastructure.Interfaces.Services;
using MassTransit;

namespace Infrastructure.Consumers;

public class UserLockedConsumer(IEmailService emailService) : IConsumer<UserLockedEvent>
{
    public async Task Consume(ConsumeContext<UserLockedEvent> context)
    {
        var evt = context.Message;
        
        await emailService.SendNotificationAsync(evt.UserName, evt.Email,"Account locked", $"Your account has been locked until {evt.LockDate}", evt.LockMessage);
    }
}