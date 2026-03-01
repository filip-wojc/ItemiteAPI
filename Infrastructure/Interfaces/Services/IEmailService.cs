using Domain.DTOs.Email;
using Domain.Entities;

namespace Infrastructure.Interfaces.Services;

public interface IEmailService
{
    Task SendConfirmationAsync(string username, string email, string emailToken);
    Task SendPasswordResetTokenAsync(User user, string passwordResetToken);
    Task SendEmailChangeTokenAsync(User user,string newEmail, string emailToken);
    Task SendGlobalNotificationAsync(List<User> recipients, string emailSubject, string title, string message);
    Task SendNotificationAsync(string userName, string userEmail, string emailSubject, string title, string message);
}