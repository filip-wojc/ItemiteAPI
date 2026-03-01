using Application.Exceptions;
using AutoMapper;
using Domain.Configs;
using Domain.DTOs.AuctionListing;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.ValueObjects;
using Infrastructure.Exceptions;
using Infrastructure.Interfaces.Repositories;
using Infrastructure.Interfaces.Services;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Listings.AuctionListings.UpdateAuctionListing;

public class UpdateAuctionListingHandler(
    IListingRepository<AuctionListing> auctionListingRepository,
    ICategoryRepository categoryRepository,
    IPhotoRepository photoRepository,
    IMediaService mediaService,
    ICacheService cacheService,
    IUnitOfWork unitOfWork,
    IMapper mapper,
    IPublishEndpoint publishEndpoint,
    ILogger<UpdateAuctionListingHandler> logger
    ) : IRequestHandler<UpdateAuctionListingCommand, AuctionListingResponse>
{
    public async Task<AuctionListingResponse> Handle(UpdateAuctionListingCommand request, CancellationToken cancellationToken)
    {
        var auctionListingToUpdate = await auctionListingRepository.GetListingByIdAsync(request.ListingId);
        if (auctionListingToUpdate == null)
        {
            throw new NotFoundException("Auction listing with id " + request.ListingId + " not found");
        }
        
        if (auctionListingToUpdate.OwnerId != request.UserId)
        {
            throw new ForbiddenException("You are not allowed to update this auction");
        }
        
        if (CalculateFinalPhotoCount(request) > 6)
        {
            throw new BadRequestException("An auction listing can have a maximum of 6 photos.");
        }

        
        auctionListingToUpdate.Name = request.UpdateDto.Name;
        auctionListingToUpdate.Description = request.UpdateDto.Description;

        var currentDateEnds = auctionListingToUpdate.DateEnds;
        auctionListingToUpdate.DateEnds = request.UpdateDto.DateEnds ?? currentDateEnds;

        if (request.UpdateDto.StartingBid != null)
        {
            if (auctionListingToUpdate.CurrentBid != null)
            {
                throw new BadRequestException("You cannot update starting bid, because someone already placed a bid");
            }
            auctionListingToUpdate.StartingBid = request.UpdateDto.StartingBid.Value;
        }
        
        if (request.UpdateDto.Location == null || !IsLocationComplete(request.UpdateDto.Location))
        {
            var ownerLoc = auctionListingToUpdate.Owner.Location;
            if (ownerLoc == null || !IsLocationComplete(ownerLoc))
            {
                throw new BadRequestException("Location is required. Please provide location or set your profile location.");
            }
   
            auctionListingToUpdate.Location.Longitude = ownerLoc.Longitude;
            auctionListingToUpdate.Location.Latitude = ownerLoc.Latitude;
            auctionListingToUpdate.Location.Country = ownerLoc.Country;
            auctionListingToUpdate.Location.City = ownerLoc.City;
            auctionListingToUpdate.Location.State = ownerLoc.State;
        }
        else
        {
            var loc = request.UpdateDto.Location;
            auctionListingToUpdate.Location.Longitude = loc.Longitude;
            auctionListingToUpdate.Location.Latitude = loc.Latitude;
            auctionListingToUpdate.Location.Country = loc.Country;
            auctionListingToUpdate.Location.City = loc.City;
            auctionListingToUpdate.Location.State = loc.State;
        }
        
        var category = await categoryRepository.GetByIdAsync(request.UpdateDto.CategoryId);
        if (category == null)
        {
            throw new NotFoundException("Category not found");
        }

        if (await categoryRepository.IsParentCategory(request.UpdateDto.CategoryId))
        {
            throw new BadRequestException("You can't assign parent category to auction listing");
        }

        var directParents = await categoryRepository.GetAllParentsRelatedToCategory(category);
        directParents.Add(category);
        
        auctionListingToUpdate.Categories = directParents;

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var allListingPhotos = auctionListingToUpdate.ListingPhotos.ToList();
            
            if (request.UpdateDto.ExistingPhotoIds.Any(id => allListingPhotos.All(lp => lp.PhotoId != id)))
            {
                throw new BadRequestException("One or more of the provided ExistingPhotoIds do not exist in this listing.");
            }
            
            var photosToDelete = allListingPhotos
                .Where(p => !request.UpdateDto.ExistingPhotoIds.Contains(p.PhotoId))
                .ToList();

            foreach (var lp in photosToDelete)
            {
                var result = await mediaService.DeleteImageAsync(lp.Photo.PublicId);
                if (result.Error != null)
                    throw new CloudinaryException($"Failed to delete photo {lp.Photo.Id}: {result.Error.Message}");

                await photoRepository.DeletePhotoAsync(lp.PhotoId);
                auctionListingToUpdate.ListingPhotos.Remove(lp);
            }
            
            for (int i = 0; i < request.UpdateDto.ExistingPhotoIds.Count; i++)
            {
                var photoId = request.UpdateDto.ExistingPhotoIds[i];
                var newOrder = request.UpdateDto.ExistingPhotoOrders[i];
                
                var listingPhoto = auctionListingToUpdate.ListingPhotos
                    .FirstOrDefault(lp => lp.PhotoId == photoId);
        
                if (listingPhoto != null)
                {
                    listingPhoto.Order = newOrder;
                }
            }
                
           
            var savedPhotosPublicIds = new List<string>();
            for (int i = 0; i < request.NewImages.Count; i++)
            {
                var image = request.NewImages[i];
                var uploadResult = await mediaService.UploadPhotoAsync(image.File);
                if (uploadResult.Error != null)
                {
                    foreach (var savedPhoto in savedPhotosPublicIds)
                    {
                        await mediaService.DeleteImageAsync(savedPhoto);
                    }
                    throw new CloudinaryException(uploadResult.Error.Message);
                }
                
                savedPhotosPublicIds.Add(uploadResult.PublicId);
                
                var photo = new Photo
                {
                    FileName = image.File.FileName, Url = uploadResult.SecureUrl.AbsoluteUri, PublicId = uploadResult.PublicId
                };
                
                await photoRepository.AddPhotoAsync(photo);
                
                var listingPhoto = new ListingPhoto
                {
                    Photo = photo, Order = image.Order
                };
                auctionListingToUpdate.ListingPhotos.Add(listingPhoto);
            }

            auctionListingRepository.UpdateListing(auctionListingToUpdate);
            await unitOfWork.CommitTransactionAsync();

            await cacheService.RemoveByPatternAsync($"{CacheKeys.LISTINGS}*");
            await cacheService.RemoveAsync($"{CacheKeys.AUCTION_LISTING}{request.ListingId}");

            var followers = await auctionListingRepository.GetListingFollowersAsync(request.ListingId);
            var auctionListingResponse = mapper.Map<AuctionListingResponse>(auctionListingToUpdate);

            await publishEndpoint.Publish(new ListingUpdatedEvent
            {
                ListingId = auctionListingToUpdate.Id,
                ListingName = auctionListingToUpdate.Name,
                OwnerId = request.UserId,
                ResourceType = ResourceType.Auction.ToString(),
                ListingPhotoUrl = auctionListingToUpdate.ListingPhotos.FirstOrDefault(lp => lp.Order == 1)?.Photo.Url,
                FollowerIds = followers.Select(f => f.Id).ToList()
            }, cancellationToken);

            return auctionListingResponse;
        }
        catch (Exception ex)
        {
            await unitOfWork.RollbackTransactionAsync();
            logger.LogError(ex, $"Error when updating {request.ListingId}: {ex.Message}");
            throw;
        }
    }
    private bool IsLocationComplete(Location? location)
    {
        if (location == null) return false;
    
        return location.Longitude.HasValue 
               && location.Latitude.HasValue 
               && !string.IsNullOrWhiteSpace(location.Country) 
               && !string.IsNullOrWhiteSpace(location.City) 
               && !string.IsNullOrWhiteSpace(location.State);
    }
    
    private int CalculateFinalPhotoCount(UpdateAuctionListingCommand request)
    {
        int newImages = request.NewImages?.Count ?? 0;
        
        if (request.UpdateDto.ExistingPhotoIds == null || request.UpdateDto.ExistingPhotoOrders == null)
            return 1 + newImages;

        int existing = request.UpdateDto.ExistingPhotoIds.Count;
        return existing + newImages;
    }
}