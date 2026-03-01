using Domain.Entities;
using Infrastructure.Database;
using Infrastructure.Exceptions;
using Infrastructure.Interfaces.Repositories;
using Infrastructure.Interfaces.Services;
using Infrastructure.Repositories;
using Infrastructure.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Stripe;
using TokenService = Infrastructure.Services.TokenService;

namespace Infrastructure.Extensions;

public static class InfrastructureExtensions
{
    public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ItemiteDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("PostgreSQL")
                              ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."));
        });
        services.AddSignalR();
        
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IListingRepository<ProductListing>, ListingRepository<ProductListing>>();
        services.AddScoped<IListingRepository<AuctionListing>, ListingRepository<AuctionListing>>();
        services.AddScoped<IListingRepository<ListingBase>, ListingRepository<ListingBase>>();
        services.AddScoped<IBidRepository, BidRepository>();

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis")
                                    ?? throw new InvalidOperationException("Connection 'Redis' not found.");
            options.InstanceName = "itemite_";
        });
        
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = configuration.GetConnectionString("Redis")
                                   ?? throw new InvalidOperationException("Connection 'Redis' not found.");
            return ConnectionMultiplexer.Connect(connectionString);
        });

        services.RegisterMassTransit(configuration);
        
        StripeConfiguration.ApiKey = configuration["StripeSettings:SecretKey"] ??
                                     throw new ConfigException("Stripe SecretKey missing");
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>(); 
        services.AddHttpContextAccessor(); // to access user-agent/ip address/device id in controllers easier
        services.AddScoped<IRequestContextService, RequestContextService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPhotoRepository, PhotoRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IBroadcastService, BroadcastService>();
        services.AddScoped<ILIstingViewRepository, ListingViewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IStripeConnectService, StripeConnectService>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IDisputeRepository, DisputeRepository>();
        services.AddScoped<IBannerRepository, BannerRepository>();
        services.AddHostedService<ExpiredFeaturedListingsCleanupService>();
        services.AddHostedService<ArchiveExpiredListingsService>();
        services.AddHostedService<OldListingViewsCleanupService>();
        services.AddHostedService<PaymentTransferBackgroundService>();
        services.AddHostedService<AuctionCompletionBackgroundService>();
    }
}

