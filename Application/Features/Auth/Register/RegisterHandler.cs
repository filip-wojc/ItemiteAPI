using Application.Exceptions;
using AutoMapper;
using Domain.Configs;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Infrastructure.Exceptions;
using Infrastructure.Interfaces.Services;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Application.Features.Auth.Register;

public class RegisterHandler(
    UserManager<User> userManager,
    IMapper mapper,
    IPublishEndpoint publishEndpoint,
    IOptions<AuthSettings> authSettings
    ) : IRequestHandler<RegisterCommand, int>
{
    public async Task<int> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var user = mapper.Map<User>(request.registerDto);
        
        var result = await userManager.CreateAsync(user, request.registerDto.Password);
        
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            throw new UserRegistrationException("Registration failed", errors);
        }

        var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var tokenExpirationInMinutes = authSettings.Value.EmailTokenLifespanInMinutes;
        user.EmailConfirmationTokenExpirationDate = DateTime.UtcNow.AddMinutes(tokenExpirationInMinutes);
        user.AuthProvider = AuthProvider.Email;
        
        await userManager.AddToRolesAsync(user, [nameof(Roles.User)]);
        await userManager.UpdateAsync(user);

        await publishEndpoint.Publish(new UserRegisteredEvent
        {
            Email = user.Email!,
            UserName = user.UserName!,
            EmailToken = emailToken
        }, cancellationToken);
        
        return user.Id;
    }
    
}