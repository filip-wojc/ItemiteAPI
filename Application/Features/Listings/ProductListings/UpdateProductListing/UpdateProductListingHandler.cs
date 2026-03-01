using Application.Exceptions;
using AutoMapper;
using Domain.Configs;
using Domain.DTOs.ProductListing;
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

namespace Application.Features.Listings.ProductListings.UpdateProductListing;

public class UpdateProductListingHandler(
    IListingRepository<ProductListing> productListingRepository,
    ICategoryRepository categoryRepository,
    IPhotoRepository photoRepository,
    IMediaService mediaService,
    ICacheService cacheService,
    IUnitOfWork unitOfWork,
    IMapper mapper,
    IPublishEndpoint publishEndpoint,
    ILogger<UpdateProductListingHandler> logger
    ) : IRequestHandler<UpdateProductListingCommand, ProductListingResponse>
{
    public async Task<ProductListingResponse> Handle(UpdateProductListingCommand request, CancellationToken cancellationToken)
    {
        var productListingToUpdate = await productListingRepository.GetListingByIdAsync(request.ListingId);
        if (productListingToUpdate == null)
        {
            throw new NotFoundException("Product listing with id " + request.ListingId + " not found");
        }
        
        if (productListingToUpdate.OwnerId != request.UserId)
        {
            throw new ForbiddenException("You are not allowed to update this product");
        }
        
        if (request.UpdateDto.ExistingPhotoIds.Count + request.NewImages.Count > 6)
        {
            throw new BadRequestException("An product listing can have a maximum of 6 photos.");
        }
        
        productListingToUpdate.Name = request.UpdateDto.Name;
        productListingToUpdate.Description = request.UpdateDto.Description;
        productListingToUpdate.Price = request.UpdateDto.Price;
        if (request.UpdateDto.Location == null || !IsLocationComplete(request.UpdateDto.Location))
        {
            var ownerLoc = productListingToUpdate.Owner.Location;
            if (ownerLoc == null || !IsLocationComplete(ownerLoc))
            {
                throw new BadRequestException("Location is required. Please provide location or set your profile location.");
            }
   
            productListingToUpdate.Location.Longitude = ownerLoc.Longitude;
            productListingToUpdate.Location.Latitude = ownerLoc.Latitude;
            productListingToUpdate.Location.Country = ownerLoc.Country;
            productListingToUpdate.Location.City = ownerLoc.City;
            productListingToUpdate.Location.State = ownerLoc.State;
        }
        else
        {
            var loc = request.UpdateDto.Location;
            productListingToUpdate.Location.Longitude = loc.Longitude;
            productListingToUpdate.Location.Latitude = loc.Latitude;
            productListingToUpdate.Location.Country = loc.Country;
            productListingToUpdate.Location.City = loc.City;
            productListingToUpdate.Location.State = loc.State;
        }
        productListingToUpdate.IsNegotiable = request.UpdateDto.IsNegotiable ?? false;
        
        var category = await categoryRepository.GetByIdAsync(request.UpdateDto.CategoryId);
        if (category == null)
        {
            throw new NotFoundException("Category not found");
        }

        if (await categoryRepository.IsParentCategory(request.UpdateDto.CategoryId))
        {
            throw new BadRequestException("You can't assign parent category to product listing");
        }

        var directParents = await categoryRepository.GetAllParentsRelatedToCategory(category);
        directParents.Add(category);
        
        productListingToUpdate.Categories = directParents;

        await unitOfWork.BeginTransactionAsync();
        try
        {
            var allListingPhotos = productListingToUpdate.ListingPhotos.ToList();
            
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
                productListingToUpdate.ListingPhotos.Remove(lp);
            }
            
            for (int i = 0; i < request.UpdateDto.ExistingPhotoIds.Count; i++)
            {
                var photoId = request.UpdateDto.ExistingPhotoIds[i];
                var newOrder = request.UpdateDto.ExistingPhotoOrders[i];
                
                var listingPhoto = productListingToUpdate.ListingPhotos
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
                productListingToUpdate.ListingPhotos.Add(listingPhoto);
            }
            

            productListingRepository.UpdateListing(productListingToUpdate);
            await unitOfWork.CommitTransactionAsync();

            await cacheService.RemoveByPatternAsync($"{CacheKeys.LISTINGS}*");
            await cacheService.RemoveAsync($"{CacheKeys.PRODUCT_LISTING}{request.ListingId}");
            
            var productListingResponse = mapper.Map<ProductListingResponse>(productListingToUpdate);
            var followers = await productListingRepository.GetListingFollowersAsync(request.ListingId);

            await publishEndpoint.Publish(new ListingUpdatedEvent
            {
                ListingId = productListingToUpdate.Id,
                ListingName = productListingToUpdate.Name,
                OwnerId = request.UserId,
                ResourceType = ResourceType.Product.ToString(),
                ListingPhotoUrl = productListingToUpdate.ListingPhotos.FirstOrDefault(lp => lp.Order == 1)?.Photo.Url,
                FollowerIds = followers.Select(f => f.Id).ToList()
            }, cancellationToken);

            return productListingResponse;
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
    
    private int CalculateFinalPhotoCount(UpdateProductListingCommand request)
    {
        int newImages = request.NewImages?.Count ?? 0;
        
        if (request.UpdateDto.ExistingPhotoIds == null || request.UpdateDto.ExistingPhotoOrders == null)
            return 1 + newImages;

        int existing = request.UpdateDto.ExistingPhotoIds.Count;
        return existing + newImages;
    }
}