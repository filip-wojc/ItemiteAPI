using Domain.Events;
using Infrastructure.Interfaces.Services;
using MassTransit;

namespace Infrastructure.Consumers;

public class UserUnlockedConsumer(IEmailService emailService) : IConsumer<UserUnlockedEvent>
{
    public async Task Consume(ConsumeContext<UserUnlockedEvent> context)
    {
        var evt = context.Message;
        
        await emailService.SendNotificationAsync(evt.UserName, evt.Email,"Account unlocked", $"Your account has been unlocked!", evt.UnlockMessage);
    }
}