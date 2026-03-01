using Domain.Configs;
using Domain.DTOs.Email;
using Domain.Entities;
using FluentEmail.Core;
using Infrastructure.Exceptions;
using Infrastructure.Interfaces.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class EmailService(
    IOptions<RedirectSettings> redirectSettings,
    IFluentEmailFactory fluentEmailFactory
) : IEmailService
{
    public async Task SendConfirmationAsync(string userName, string userEmail, string emailToken)
    {
        
        var queryParam = new Dictionary<string, string>
        {
            { "token", emailToken },
            { "email", userEmail }
        };

        var emailConfirmationUrl = redirectSettings.Value.EmailVerificationUrl;

        var confirmationLink = QueryHelpers.AddQueryString(emailConfirmationUrl, queryParam!);

        var template = "Helpers/EmailTemplates/EmailConfirmation.cshtml";
        
        var email = fluentEmailFactory.Create();

        var sendResponse = await email
            .To(userEmail)
            .Subject("Itemite email confirmation")
            .UsingTemplateFromFile(template, new EmailConfirmationModel
            {
                UserName = userName,
                ConfirmationLink = confirmationLink
            })
            .SendAsync();

        if (!sendResponse.Successful)
        {
            throw new EmailException("Error while sending confirmation email", sendResponse.ErrorMessages.ToList());
        }
    }

    public async Task SendPasswordResetTokenAsync(User user, string passwordResetToken)
    {
        var queryParam = new Dictionary<string, string>
        {
            { "token", passwordResetToken },
            { "email", user.Email! }
        };

        var passwordResetUrl = redirectSettings.Value.PasswordResetUrl;

        var passwordResetLink = QueryHelpers.AddQueryString(passwordResetUrl, queryParam!);

        var template = "Helpers/EmailTemplates/PasswordReset.cshtml";
        
        var email = fluentEmailFactory.Create();

        var sendResponse = await email
            .To(user.Email!)
            .Subject("Itemite password reset")
            .UsingTemplateFromFile(template, new EmailConfirmationModel
            {
                UserName = user.UserName!,
                ConfirmationLink = passwordResetLink
            })
            .SendAsync();

        if (!sendResponse.Successful)
        {
            throw new EmailException("Error while sending reset password email", sendResponse.ErrorMessages.ToList());
        }
    }

    public async Task SendEmailChangeTokenAsync(User user, string newEmail, string emailChangeToken)
    {
        var queryParam = new Dictionary<string, string>
        {
            { "token", emailChangeToken },
            { "currentEmail", user.Email! }
        };

        var emailChangeConfirmationUrl = redirectSettings.Value.EmailChangeConfirmationUrl;

        var confirmationLink = QueryHelpers.AddQueryString(emailChangeConfirmationUrl, queryParam!);

        var template = "Helpers/EmailTemplates/EmailChangeConfirmation.cshtml";
        
        var email = fluentEmailFactory.Create();

        var sendResponse = await email
            .To(newEmail)
            .Subject("Itemite email change")
            .UsingTemplateFromFile(template, new EmailChangeConfirmationModel
            {
                UserName = user.UserName!,
                NewEmail = newEmail,
                ConfirmationLink = confirmationLink
            })
            .SendAsync();

        if (!sendResponse.Successful)
        {
            throw new EmailException("Error while sending email change confirmation", sendResponse.ErrorMessages.ToList());
        }
    }

    public async Task SendGlobalNotificationAsync(List<User> recipients, string emailSubject, string title, string message)
    {
        // TODO: Prepare email template
        var template = "Helpers/EmailTemplates/Notification.cshtml";

        foreach (var recipient in recipients)
        {
            var email = fluentEmailFactory.Create();
            
            var sendResponse = await email
                .To(recipient.Email)
                .Subject(emailSubject)
                .UsingTemplateFromFile(template, new EmailNotificationModel
                {
                    Title = title,
                    Message = message,
                    RecipientUsername = recipient.UserName!
                })
                .SendAsync();

            if (!sendResponse.Successful)
            {
                throw new EmailException("Error while sending global notification email", sendResponse.ErrorMessages.ToList());
            }
        }
    }

    public async Task SendNotificationAsync(string userName, string userEmail, string emailSubject, string title, string message)
    {
        // TODO: Prepare email template
        var template = "Helpers/EmailTemplates/Notification.cshtml";
        
        var email = fluentEmailFactory.Create();
        
        var sendResponse = await email
            .To(userEmail)
            .Subject(emailSubject)
            .UsingTemplateFromFile(template, new EmailNotificationModel
            {
                Title = title, Message = message, RecipientUsername = userName
            })
            .SendAsync();

        if (!sendResponse.Successful)
        {
            throw new EmailException("Error while sending notification email", sendResponse.ErrorMessages.ToList());
        }
        
    }
}