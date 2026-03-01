using Application.Exceptions;
using Domain.Entities;
using Domain.Events;
using Infrastructure.Exceptions;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Features.Users.LockUser;

public class LockUserHandler(
    UserManager<User> userManager,
    IPublishEndpoint publishEndpoint
    ) : IRequestHandler<LockUserCommand>
{
    public async Task Handle(LockUserCommand request, CancellationToken cancellationToken)
    {
        var userToLockout = await userManager.FindByIdAsync(request.LockUserDto.UserToLockoutId.ToString());
        if (userToLockout == null)
        {
            throw new Exception("User not found");
        }

        if (!userToLockout.LockoutEnabled)
        {
            throw new ForbiddenException("User can't be locked");
        }
        
        var lockoutDate = request.LockUserDto.LockoutEnd ?? DateTime.UtcNow.AddDays(3);
        userToLockout.LockoutEnd = lockoutDate;
        
        var result = await userManager.UpdateAsync(userToLockout);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            throw new BadRequestException("Failed to lock user", errors);
        }

        await publishEndpoint.Publish(new UserLockedEvent
        {
            UserName = userToLockout.UserName!,
            Email = userToLockout.Email!,
            LockDate = lockoutDate.ToString("g"),
            LockMessage = request.LockUserDto.LockoutMessage!
        }, cancellationToken);
    }
}