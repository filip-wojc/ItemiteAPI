using AutoMapper;
using Domain.Auth;
using Domain.Configs;
using Domain.DTOs.User;
using Domain.Entities;
using Infrastructure.Exceptions;
using Infrastructure.Interfaces.Services;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Features.Auth.Login;

public class LoginHandler(
    ITokenService tokenService,
    UserManager<User> userManager,
    IOptions<AuthSettings> authSettings,
    IEmailService emailService,
    IHttpContextAccessor contextAccessor,
    IMapper mapper,
    ILogger<LoginHandler> logger
    ) : IRequestHandler<LoginCommand, UserBasicResponse>
{
    public async Task<UserBasicResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.loginDto.Email);
        if (user == null)
        {
            throw new BadRequestException("User not found");
        }

        if (!await userManager.CheckPasswordAsync(user, request.loginDto.Password))
        {
            throw new UnauthorizedException("Invalid credentials");
        }
        
        var settings = authSettings.Value;
        
        // checks the appsettings.json if email confirmation is needed to log in
        var isEmailConfRequired = settings.IsEmailConfirmationRequired;
        
        if (isEmailConfRequired && !await userManager.IsEmailConfirmedAsync(user))
        {
            if (user.EmailConfirmationTokenExpirationDate < DateTime.UtcNow)
            {
                var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var tokenExpirationInMinutes = settings.EmailTokenLifespanInMinutes;
                user.EmailConfirmationTokenExpirationDate = DateTime.UtcNow.AddMinutes(tokenExpirationInMinutes);
                try
                {
                    await emailService.SendConfirmationAsync(user.UserName!, user.Email!, emailToken);
                    await userManager.UpdateAsync(user);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,$"Error while sending email confirmation token: {ex.Message}");
                    throw new EmailException("Error while sending confirmation email");
                }
                throw new UnauthorizedException("Email is not confirmed. New confirmation link has been sent.");
            }
            
            throw new UnauthorizedException("Email is not confirmed. Check your email for the confirmation link");
            
        }
        
        if (await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException("Your account has been locked");
        }

        var tokens = await tokenService.GenerateTokenPairAsync(
            user,
            request.IpAddress,
            request.DeviceId,
            request.UserAgent
        );
        
        tokenService.SetTokensInsideCookie(tokens, contextAccessor.HttpContext!);

        return mapper.Map<UserBasicResponse>(user);
    }
}