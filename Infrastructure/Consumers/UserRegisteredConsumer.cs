using Domain.Events;
using Infrastructure.Exceptions;
using Infrastructure.Interfaces.Services;
using MassTransit;

namespace Infrastructure.Consumers;

public class UserRegisteredConsumer(
    IEmailService emailService
    ) : IConsumer<UserRegisteredEvent>
{
    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        try
        {
            await emailService.SendConfirmationAsync(context.Message.UserName, context.Message.Email, context.Message.EmailToken);
        }
        catch (Exception e)
        {
            throw new EmailException("Error while sending confirmation email", [e.Message]);
        }
    }
}
