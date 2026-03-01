using Domain.Configs;
using Domain.DTOs.Payments;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Extensions;
using Infrastructure.Exceptions;
using Infrastructure.Interfaces.Repositories;
using Infrastructure.Interfaces.Services;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace Application.Features.Payments.PurchaseProduct;

/// <summary>
/// BUYER Handler to purchase the listed product, will create a payment entity and mark the listing as IsSold
/// This will create a charge for the user's payment method.
/// Seller needs to be onboarded on stripe for the purchase to work
/// </summary>
public class PurchaseProductHandler(
    IListingRepository<ProductListing> productListingRepository,
    IUserRepository userRepository,
    IPaymentRepository paymentRepository,
    IStripeConnectService stripeConnectService,
    IUnitOfWork unitOfWork,
    ICacheService cacheService,
    IPublishEndpoint publishEndpoint,
    IOptions<PaymentSettings> paymentSettings,
    ILogger<PurchaseProductCommand> logger
) : IRequestHandler<PurchaseProductCommand, PurchaseProductResponse>
{
    public async Task<PurchaseProductResponse> Handle(PurchaseProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = await productListingRepository.GetListingByIdAsync(request.ProductListingId);
        if (product == null)
        {
            throw new NotFoundException($"Product with id {request.ProductListingId} not found");
        }

        if (product.IsArchived)
        {
            throw new BadRequestException("This product is no longer available");
        }

        if (product.IsSold)
        {
            throw new BadRequestException("This product has already been sold");
        }

        var buyer = await userRepository.GetUserWithProfilePhotoAsync(request.BuyerId);

        if (buyer == null)
        {
            throw new NotFoundException($"Buyer with id {request.BuyerId} not found");
        }
        
        if (product.OwnerId == request.BuyerId)
        {
            throw new BadRequestException("You cannot purchase your own product");
        }

        if (string.IsNullOrEmpty(product.Owner.StripeConnectAccountId))
        {
            throw new BadRequestException("Seller has not set up their payment account yet");
        }

        var isSellerOnboarded = await stripeConnectService.IsAccountFullyOnboardedAsync(
            product.Owner.StripeConnectAccountId);

        if (!isSellerOnboarded)
        {
            throw new BadRequestException("Seller's payment account is not fully set up yet");
        }

        var userSpecificPrice = await productListingRepository.GetUserListingPriceAsync(
            product.Id,
            request.BuyerId);

        var finalPrice = userSpecificPrice?.Price ?? product.Price;

        var platformFeePercentage = paymentSettings.Value.PlatformFeePercentage;
        var platformFeeAmount = finalPrice * (platformFeePercentage / 100);
        var sellerAmount = finalPrice - platformFeeAmount;

        var paymentMetadata = new Dictionary<string, string>
        {
            { "product_id", product.Id.ToString() },
            { "product_name", product.Name },
            { "seller_id", product.OwnerId.ToString() },
            { "buyer_id", request.BuyerId.ToString() },
            { "platform_fee", platformFeeAmount.ToString("F2") },
            { "payment_type", "product_purchase" }
        };


        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var paymentIntent = await stripeConnectService.CreatePaymentIntentAsync(
                amount: finalPrice,
                currency: "pln",
                paymentMethodId: request.PaymentMethodId,
                description: $"Purchase: {product.Name}",
                returnUrl: paymentSettings.Value.PurchaseCompleteUrl,
                metadata: paymentMetadata,
                captureMethod: CaptureMethods.AUTOMATIC
            );

            var paymentIntentStatus = PaymentIntentStatusExtensions.FromStripeStatus(paymentIntent.Status);

            var payment = new Payment
            {
                StripePaymentIntentId = paymentIntent.Id,
                PaymentIntentClientSecret = paymentIntent.ClientSecret,
                PaymentIntentStatus = paymentIntentStatus,
                StripeChargeId = paymentIntent.LatestChargeId,
                TotalAmount = finalPrice,
                PlatformFeePercentage = platformFeePercentage,
                PlatformFeeAmount = platformFeeAmount,
                SellerAmount = sellerAmount,
                Currency = "pln",
                ListingId = product.Id,
                BuyerId = request.BuyerId,
                SellerId = product.OwnerId,
                Status = PaymentStatus.Pending, // Charged, waiting for transfer to seller
                TransferTrigger = TransferTrigger.TimeBased,
                ScheduledTransferDate = DateTime.UtcNow.AddDays(paymentSettings.Value.TransferDelayDays),
                ChargeDate = DateTime.UtcNow,
                Notes = "Product purchase - automatic capture"
            };

            await paymentRepository.AddAsync(payment);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            
            product.IsSold = true;
            product.IsArchived = true;
            product.PaymentId = payment.Id;
            productListingRepository.UpdateListing(product);
            
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            await cacheService.RemoveAsync($"{CacheKeys.PRODUCT_LISTING}{product.Id}");
            await cacheService.RemoveByPatternAsync($"{CacheKeys.LISTINGS}*");

            logger.LogInformation(
                "Product purchase successful - Product: {ProductId}, Buyer: {BuyerId}, " +
                "Amount: {Amount} PLN, PaymentIntent: {PaymentIntentId}",
                product.Id, request.BuyerId, finalPrice, paymentIntent.Id);

            await publishEndpoint.Publish(new ProductPurchasedEvent
            {
                ProductId = product.Id,
                ProductName = product.Name,
                SellerId = product.OwnerId,
                BuyerId = request.BuyerId,
                BuyerUserName = buyer.UserName!,
                ProductPhotoUrl = product.ListingPhotos.FirstOrDefault(lp => lp.Order == 1)?.Photo.Url
            }, cancellationToken);

            return new PurchaseProductResponse
            {
                PaymentId = payment.Id,
                Message =
                    $"Purchase successful. Money will be transferred to the seller within {paymentSettings.Value.TransferDelayDays} days"
            };
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync();
            logger.LogError(ex,
                "Error processing purchase for product {ProductId}: {Message}",
                request.ProductListingId, ex.Message);
            throw;
        }
    }
}