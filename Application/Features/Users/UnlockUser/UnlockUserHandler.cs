using Application.Exceptions;
using Domain.Entities;
using Domain.Events;
using Infrastructure.Exceptions;
using Infrastructure.Interfaces.Services;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Features.Users.UnlockUser;

public class UnlockUserHandler(
    UserManager<User> userManager,
    IPublishEndpoint publishEndpoint
    ) : IRequestHandler<UnlockUserCommand>
{
    public async Task Handle(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        var userToUnlock = await userManager.FindByIdAsync(request.UnlockUserDto.UserId.ToString());
        if (userToUnlock == null)
        {
            throw new Exception("User not found");
        }

        if (userToUnlock.LockoutEnd == null)
        {
            throw new ForbiddenException("User is not locked");
        }

        userToUnlock.LockoutEnd = null;
        var result = await userManager.UpdateAsync(userToUnlock);
        
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            throw new BadRequestException("Failed to unlock user", errors);
        }

        await publishEndpoint.Publish(new UserUnlockedEvent
        {
            UserName = userToUnlock.UserName!,
            Email = userToUnlock.Email!,
            UnlockMessage = request.UnlockUserDto.UnlockMessage ?? string.Empty
        });
    }
}