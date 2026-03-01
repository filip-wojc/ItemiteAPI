using Domain.Configs;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Extensions;
using Infrastructure.Exceptions;
using Infrastructure.Interfaces.Repositories;
using Infrastructure.Interfaces.Services;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Features.Listings.AuctionListings.PlaceBid;

public class PlaceBidHandler(
    IBidRepository bidRepository,
    IListingRepository<AuctionListing> auctionListingRepository,
    IPaymentRepository paymentRepository,
    IStripeConnectService stripeConnectService,
    ICacheService cacheService,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    UserManager<User> userManager,
    IOptions<PaymentSettings> paymentSettings,
    ILogger<PlaceBidHandler> logger
) : IRequestHandler<PlaceBidCommand, int>
{
    public async Task<int> Handle(PlaceBidCommand request, CancellationToken cancellationToken)
    {
        var auction = await auctionListingRepository.GetListingByIdAsync(request.AuctionId);
        if (auction == null)
        {
            throw new NotFoundException($"Auction with id: {request.AuctionId} not found");
        }

        var bidder = await userManager.FindByIdAsync(request.UserId.ToString());
        if (bidder == null)
        {
            throw new NotFoundException($"User with id: {request.UserId} not found");
        }

        if (auction.OwnerId == request.UserId)
        {
            throw new BadRequestException("You cannot place a bid on an auction you have created");
        }

        if (auction.IsArchived)
        {
            throw new BadRequestException("You cannot place a bid on an archived auction");
        }

        if (auction.DateEnds <= DateTime.UtcNow)
        {
            throw new BadRequestException("Auction has ended");
        }

        // ONBOARDING CHECKS
        if (string.IsNullOrEmpty(auction.Owner.StripeConnectAccountId))
        {
            throw new BadRequestException("Seller has not set up their payment account yet");
        }

        var isSellerOnboarded = await stripeConnectService.IsAccountFullyOnboardedAsync(
            auction.Owner.StripeConnectAccountId);

        if (!isSellerOnboarded)
        {
            throw new BadRequestException("Seller's payment account is not fully set up yet");
        }
        ////////////////////


        var formerHighestBid = await bidRepository.GetCurrentHighestBid(auction.Id);


        if (formerHighestBid?.BidderId == request.UserId)
        {
            throw new BadRequestException("You can't outbid your own bid");
        }
        
        var currentHighestBidValue = auction.CurrentBid ?? auction.StartingBid;

        if (request.BidDto.Price <= currentHighestBidValue)
        {
            throw new BadRequestException("You cannot place an equal or lower bid than current highest bid");
        }

        var platformFeePercentage = paymentSettings.Value.PlatformFeePercentage;
        var platformFeeAmount = request.BidDto.Price * (platformFeePercentage / 100);
        var sellerAmount = request.BidDto.Price - platformFeeAmount;


        await unitOfWork.BeginTransactionAsync();
        try
        {
            var paymentIntentMetadata = new Dictionary<string, string>
            {
                { "auction_id", auction.Id.ToString() },
                { "auction_name", auction.Name },
                { "seller_id", auction.OwnerId.ToString() },
                { "bidder_id", request.UserId.ToString() },
                { "bid_amount", request.BidDto.Price.ToString("F2") },
                { "platform_fee", platformFeeAmount.ToString("F2") }
            };

            var paymentIntent = await stripeConnectService.CreatePaymentIntentAsync(
                amount: request.BidDto.Price,
                currency: "pln",
                paymentMethodId: request.BidDto.PaymentMethodId, // Frontend provides this
                description: $"Bid authorization for auction: {auction.Name}",
                returnUrl: paymentSettings.Value.BidCompleteUrl,
                captureMethod: CaptureMethods.MANUAL,
                metadata: paymentIntentMetadata
            );

            var paymentIntentStatus = PaymentIntentStatusExtensions.FromStripeStatus(paymentIntent.Status);

            var payment = new Payment
            {
                StripePaymentIntentId = paymentIntent.Id,
                PaymentIntentClientSecret = paymentIntent.ClientSecret,
                PaymentIntentStatus = paymentIntentStatus,
                TotalAmount = request.BidDto.Price,
                PlatformFeePercentage = platformFeePercentage,
                PlatformFeeAmount = platformFeeAmount,
                SellerAmount = sellerAmount,
                Currency = "pln",
                ListingId = auction.Id,
                BuyerId = request.UserId,
                SellerId = auction.OwnerId,
                Status = PaymentStatus.Authorized, // Authorized = held, not yet captured
                TransferTrigger = TransferTrigger.TimeBased,
                ChargeDate = DateTime.UtcNow
            };

            await paymentRepository.AddAsync(payment);
            await unitOfWork.SaveChangesAsync(cancellationToken);


            var bidToAdd = new AuctionBid
            {
                BidPrice = request.BidDto.Price,
                BidderId = request.UserId,
                AuctionId = auction.Id,
                PaymentId = payment.Id
            };
            await bidRepository.CreateBid(bidToAdd);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            auction.HighestBidId = bidToAdd.Id;
            auction.CurrentBid = bidToAdd.BidPrice;
            auctionListingRepository.UpdateListing(auction);

            // cancel the previous payment intent
            if (formerHighestBid?.PaymentId != null)
            {
                var formerPayment = await paymentRepository.FindByIdAsync(formerHighestBid.PaymentId.Value);
                if (formerPayment != null)
                {
                    // Just mark as outbid, keep PaymentIntent authorized as backup
                    formerPayment.Status = PaymentStatus.Outbid;
                    formerPayment.Notes =
                        $"Outbid by user {request.UserId} with bid {request.BidDto.Price} at {DateTime.UtcNow}";
                    paymentRepository.Update(formerPayment);

                    logger.LogInformation(
                        $"Marked payment {formerPayment.Id} as outbid (PaymentIntent kept authorized)");
                }
            }

            await unitOfWork.CommitTransactionAsync(cancellationToken);

            await cacheService.RemoveAsync($"{CacheKeys.BIDS}{auction.Id}");
            await cacheService.RemoveAsync($"{CacheKeys.AUCTION_LISTING}{auction.Id}");

            await publishEndpoint.Publish(new BidPlacedEvent
            {
                AuctionId = auction.Id,
                AuctionName = auction.Name,
                AuctionOwnerId = auction.OwnerId,
                BidderId = request.UserId,
                BidderUserName = bidder.UserName!,
                BidPrice = bidToAdd.BidPrice,
                AuctionPhotoUrl = auction.ListingPhotos.FirstOrDefault(lp => lp.Order == 1)?.Photo.Url,
                FormerHighestBidderId = formerHighestBid?.BidderId
            }, cancellationToken);

            return bidToAdd.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error when placing a bid: {ex.Message}");
            await unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}