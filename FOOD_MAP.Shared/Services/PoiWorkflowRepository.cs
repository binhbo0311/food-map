using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Shared.Services;

public sealed class PoiWorkflowRepository : IPoiWorkflowRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public PoiWorkflowRepository(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<string> GenerateNextPoiIdAsync(PoiType type, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await GenerateNextPoiIdInternalAsync(dbContext, type, cancellationToken);
    }

    public async Task<IReadOnlyList<Language>> GetAvailableLanguagesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Languages
            .AsNoTracking()
            .OrderBy(x => x.LanguageName)
            .ThenBy(x => x.LanguageCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SubmitLanguageOwnershipRequestAsync(
        int ownerUserId,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var owner = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ownerUserId, cancellationToken);

        if (owner is null)
        {
            throw new InvalidOperationException("Owner account no longer exists.");
        }

        if (owner.Role != UserRole.Owner)
        {
            throw new InvalidOperationException("Language ownership request is only available for owner accounts.");
        }

        var normalizedLanguageCode = NormalizeLanguageCode(TextInputNormalizer.NormalizeSingleLine(languageCode));
        if (string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            throw new InvalidOperationException("Language code is required.");
        }

        if (string.Equals(normalizedLanguageCode, "vi", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Vietnamese base language is already available and does not require approval.");
        }

        var language = await GetOrCreateLanguageAsync(dbContext, normalizedLanguageCode, cancellationToken);

        var latestRequest = await dbContext.LanguageOwnershipRequests
            .Where(x => x.OwnerUserId == ownerUserId && x.LanguageId == language.Id)
            .OrderByDescending(x => x.RequestedUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestRequest is not null)
        {
            if (latestRequest.Status == LanguageOwnershipRequestStatus.Approved)
            {
                return latestRequest.Id;
            }

            if (latestRequest.Status == LanguageOwnershipRequestStatus.Pending)
            {
                latestRequest.RequestedUtc = DateTimeOffset.UtcNow;
                latestRequest.ReviewedUtc = null;
                latestRequest.ReviewedByAdminUserId = null;
                latestRequest.RejectionReason = null;

                await dbContext.SaveChangesAsync(cancellationToken);
                return latestRequest.Id;
            }
        }

        var request = new LanguageOwnershipRequest
        {
            OwnerUserId = ownerUserId,
            LanguageId = language.Id,
            Status = LanguageOwnershipRequestStatus.Pending,
            RequestedUtc = DateTimeOffset.UtcNow
        };

        dbContext.LanguageOwnershipRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
        return request.Id;
    }

    public async Task<IReadOnlyList<LanguageOwnershipRequest>> GetOwnerLanguageOwnershipRequestsAsync(
        int ownerUserId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.LanguageOwnershipRequests
            .AsNoTracking()
            .Include(x => x.Language)
            .Include(x => x.ReviewedByAdminUser)
            .Where(x => x.OwnerUserId == ownerUserId)
            .OrderByDescending(x => x.RequestedUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LanguageOwnershipRequest>> GetPendingLanguageOwnershipRequestsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.LanguageOwnershipRequests
            .AsNoTracking()
            .Include(x => x.Language)
            .Include(x => x.OwnerUser)
            .Where(x => x.Status == LanguageOwnershipRequestStatus.Pending)
            .OrderBy(x => x.RequestedUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool IsSuccess, string Message)> ApproveLanguageOwnershipRequestAsync(
        int requestId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var request = await dbContext.LanguageOwnershipRequests
            .Include(x => x.Language)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (request is null)
        {
            return (false, "Language ownership request not found.");
        }

        if (request.Status != LanguageOwnershipRequestStatus.Pending)
        {
            return (false, "Language ownership request is already processed.");
        }

        request.Status = LanguageOwnershipRequestStatus.Approved;
        request.ReviewedUtc = DateTimeOffset.UtcNow;
        request.ReviewedByAdminUserId = adminUserId;
        request.RejectionReason = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        var languageCode = request.Language?.LanguageCode ?? request.LanguageId.ToString();
        return (true, $"Approved language ownership request for {languageCode}.");
    }

    public async Task<(bool IsSuccess, string Message)> RejectLanguageOwnershipRequestAsync(
        int requestId,
        int adminUserId,
        string rejectionReason,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var request = await dbContext.LanguageOwnershipRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (request is null)
        {
            return (false, "Language ownership request not found.");
        }

        if (request.Status != LanguageOwnershipRequestStatus.Pending)
        {
            return (false, "Language ownership request is already processed.");
        }

        request.Status = LanguageOwnershipRequestStatus.Rejected;
        request.ReviewedUtc = DateTimeOffset.UtcNow;
        request.ReviewedByAdminUserId = adminUserId;
        var normalizedRejectionReason = TextInputNormalizer.NormalizeNullableMultiline(rejectionReason);
        request.RejectionReason = string.IsNullOrWhiteSpace(normalizedRejectionReason)
            ? "Rejected by administrator."
            : normalizedRejectionReason;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Language ownership request rejected.");
    }

    public async Task<int> SubmitOwnerRegistrationRequestAsync(
        int userId,
        string businessName,
        string businessAddress,
        string contactPhone,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var normalizedBusinessName = TextInputNormalizer.NormalizeSingleLine(businessName);
        var normalizedBusinessAddress = TextInputNormalizer.NormalizeSingleLine(businessAddress);
        var normalizedContactPhone = TextInputNormalizer.NormalizeSingleLine(contactPhone);
        var normalizedNotes = TextInputNormalizer.NormalizeNullableMultiline(notes);

        var existingPendingRequest = await dbContext.OwnerRegistrationRequests
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Status == OwnerRegistrationStatus.Pending,
                cancellationToken);

        if (existingPendingRequest is not null)
        {
            // Khi user đang có pending request, chỉ cho phép chỉnh sửa chính request đang chờ duyệt.
            existingPendingRequest.BusinessName = normalizedBusinessName;
            existingPendingRequest.BusinessAddress = normalizedBusinessAddress;
            existingPendingRequest.ContactPhone = normalizedContactPhone;
            existingPendingRequest.Notes = normalizedNotes;
            existingPendingRequest.RequestedUtc = DateTimeOffset.UtcNow;
            existingPendingRequest.ReviewedUtc = null;
            existingPendingRequest.ReviewedByAdminUserId = null;
            existingPendingRequest.RejectionReason = null;
            existingPendingRequest.ApprovedOwnerCode = null;

            await dbContext.SaveChangesAsync(cancellationToken);
            return existingPendingRequest.Id;
        }

        var request = new OwnerRegistrationRequest
        {
            UserId = userId,
            BusinessName = normalizedBusinessName,
            BusinessAddress = normalizedBusinessAddress,
            ContactPhone = normalizedContactPhone,
            Notes = normalizedNotes,
            Status = OwnerRegistrationStatus.Pending,
            RequestedUtc = DateTimeOffset.UtcNow
        };

        dbContext.OwnerRegistrationRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return request.Id;
    }

    public async Task<OwnerRegistrationRequest?> GetLatestOwnerRegistrationRequestForUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.OwnerRegistrationRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RequestedUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool IsSuccess, string Message)> CancelPendingOwnerRegistrationRequestAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var pendingRequest = await dbContext.OwnerRegistrationRequests
            .Where(x => x.UserId == userId && x.Status == OwnerRegistrationStatus.Pending)
            .OrderByDescending(x => x.RequestedUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (pendingRequest is null)
        {
            return (false, "No pending owner registration request to cancel.");
        }

        pendingRequest.Status = OwnerRegistrationStatus.Rejected;
        pendingRequest.ReviewedUtc = DateTimeOffset.UtcNow;
        pendingRequest.ReviewedByAdminUserId = null;
        pendingRequest.RejectionReason = "Cancelled by user.";
        pendingRequest.ApprovedOwnerCode = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Pending owner registration request was cancelled.");
    }

    public async Task<IReadOnlyList<OwnerRegistrationRequest>> GetPendingOwnerRegistrationRequestsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.OwnerRegistrationRequests
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.Status == OwnerRegistrationStatus.Pending)
            .OrderBy(x => x.RequestedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool IsSuccess, string Message)> ApproveOwnerRegistrationAsync(
        int requestId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var request = await dbContext.OwnerRegistrationRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (request is null)
        {
            return (false, "Owner registration request not found.");
        }

        if (request.Status != OwnerRegistrationStatus.Pending)
        {
            return (false, "Owner registration request is already processed.");
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            return (false, "User account no longer exists.");
        }

        var ownerCode = await GenerateNextOwnerCodeAsync(dbContext, cancellationToken);

        request.Status = OwnerRegistrationStatus.Approved;
        request.ReviewedUtc = DateTimeOffset.UtcNow;
        request.ReviewedByAdminUserId = adminUserId;
        request.ApprovedOwnerCode = ownerCode;
        request.RejectionReason = null;

        user.Role = UserRole.Owner;
        user.OwnerIdentificationCode = ownerCode;
        user.OwnerApprovedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (true, $"Approved request and issued owner code {ownerCode}.");
    }

    public async Task<(bool IsSuccess, string Message)> RejectOwnerRegistrationAsync(
        int requestId,
        int adminUserId,
        string rejectionReason,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var request = await dbContext.OwnerRegistrationRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (request is null)
        {
            return (false, "Owner registration request not found.");
        }

        if (request.Status != OwnerRegistrationStatus.Pending)
        {
            return (false, "Owner registration request is already processed.");
        }

        request.Status = OwnerRegistrationStatus.Rejected;
        request.ReviewedUtc = DateTimeOffset.UtcNow;
        request.ReviewedByAdminUserId = adminUserId;
        var normalizedRejectionReason = TextInputNormalizer.NormalizeNullableMultiline(rejectionReason);
        request.RejectionReason = string.IsNullOrWhiteSpace(normalizedRejectionReason)
            ? "Rejected by administrator."
            : normalizedRejectionReason;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Owner registration request rejected.");
    }

    public async Task<string> SubmitOwnerPoiAsync(
        int ownerUserId,
        PoiType type,
        double latitude,
        double longitude,
        int activationRadius,
        int priority,
        string? qrCodeId,
        string baseLanguageCode,
        string locationName,
        string description,
        string imageUrl,
        string audioFileUrl,
        string ttsScript,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var poiId = await GenerateNextPoiIdInternalAsync(dbContext, type, cancellationToken);
        var normalizedLanguageCode = NormalizeLanguageCode(TextInputNormalizer.NormalizeSingleLine(baseLanguageCode));
        var normalizedLocationName = TextInputNormalizer.NormalizeSingleLine(locationName);
        var normalizedDescription = TextInputNormalizer.NormalizeMultiline(description);
        var normalizedImageUrl = TextInputNormalizer.NormalizeSingleLine(imageUrl);
        var normalizedAudioFileUrl = TextInputNormalizer.NormalizeSingleLine(audioFileUrl);
        var normalizedTtsScript = TextInputNormalizer.NormalizeMultiline(ttsScript);
        var normalizedQrCodeId = TextInputNormalizer.NormalizeNullableSingleLine(qrCodeId);

        await EnsureOwnerCanUseLanguageAsync(dbContext, ownerUserId, normalizedLanguageCode, cancellationToken);

        var language = await GetOrCreateLanguageAsync(dbContext, normalizedLanguageCode, cancellationToken);

        var poi = new POI
        {
            Id = poiId,
            Type = type,
            Latitude = latitude,
            Longitude = longitude,
            ActivationRadius = activationRadius,
            Priority = priority,
            QRCodeId = normalizedQrCodeId,
            ApprovalStatus = PoiApprovalStatus.Pending,
            SubmittedUtc = DateTimeOffset.UtcNow,
            OwnerId = ownerUserId
        };

        dbContext.Pois.Add(poi);

        dbContext.PoiTranslations.Add(new POITranslation
        {
            PoiId = poi.Id,
            LanguageId = language.Id,
            LocationName = normalizedLocationName,
            Description = normalizedDescription,
            ImageUrl = normalizedImageUrl,
            AudioFileUrl = normalizedAudioFileUrl,
            TtsScript = normalizedTtsScript
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return poi.Id;
    }

    public async Task<IReadOnlyList<POI>> GetOwnerPoisAsync(
        int ownerUserId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Pois
            .AsNoTracking()
            .Include(x => x.PoiTranslations)
            .Where(x => x.OwnerId == ownerUserId)
            .OrderByDescending(x => x.SubmittedUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool IsSuccess, string Message)> UpdateOwnerPoiBasicInfoAsync(
        int ownerUserId,
        string poiId,
        PoiType type,
        double latitude,
        double longitude,
        int activationRadius,
        int priority,
        string? qrCodeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return (false, "POI id is required.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var normalizedPoiId = poiId.Trim().ToUpperInvariant();
        var poi = await dbContext.Pois
            .FirstOrDefaultAsync(x => x.Id == normalizedPoiId && x.OwnerId == ownerUserId, cancellationToken);

        if (poi is null)
        {
            return (false, "POI not found or not owned by this user.");
        }

        poi.Type = type;
        poi.Latitude = latitude;
        poi.Longitude = longitude;
        poi.ActivationRadius = activationRadius;
        poi.Priority = priority;
        poi.QRCodeId = TextInputNormalizer.NormalizeNullableSingleLine(qrCodeId);

        // Owner chỉnh sửa thông tin cơ bản sẽ quay lại trạng thái chờ admin duyệt.
        poi.ApprovalStatus = PoiApprovalStatus.Pending;
        poi.SubmittedUtc = DateTimeOffset.UtcNow;
        poi.ReviewedByAdminUserId = null;
        poi.ReviewedUtc = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, $"POI {poi.Id} was updated and moved to pending approval.");
    }

    public async Task<IReadOnlyList<FoodItem>> GetOwnerFoodItemsAsync(
        int ownerUserId,
        string poiId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return Array.Empty<FoodItem>();
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedPoiId = poiId.Trim().ToUpperInvariant();

        var hasPermission = await dbContext.Pois
            .AsNoTracking()
            .AnyAsync(x => x.Id == normalizedPoiId && x.OwnerId == ownerUserId, cancellationToken);

        if (!hasPermission)
        {
            return Array.Empty<FoodItem>();
        }

        return await dbContext.FoodItems
            .AsNoTracking()
            .Where(x => x.PoiId == normalizedPoiId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> SaveOwnerFoodItemAsync(
        int ownerUserId,
        string poiId,
        int? foodItemId,
        string name,
        string? description,
        decimal price,
        string currency,
        bool isAvailable,
        int displayOrder,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            throw new InvalidOperationException("POI id is required.");
        }

        var normalizedName = TextInputNormalizer.NormalizeSingleLine(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("Food item name is required.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedPoiId = poiId.Trim().ToUpperInvariant();

        var poi = await dbContext.Pois
            .FirstOrDefaultAsync(x => x.Id == normalizedPoiId && x.OwnerId == ownerUserId, cancellationToken);

        if (poi is null)
        {
            throw new InvalidOperationException("POI not found or not owned by this user.");
        }

        if (poi.Type != PoiType.Food)
        {
            throw new InvalidOperationException("Food items can only be managed for food POIs.");
        }

        var normalizedCurrencyInput = TextInputNormalizer.NormalizeSingleLine(currency);
        var normalizedCurrency = string.IsNullOrWhiteSpace(normalizedCurrencyInput)
            ? "VND"
            : normalizedCurrencyInput.ToUpperInvariant();

        var normalizedDescription = TextInputNormalizer.NormalizeNullableMultiline(description);

        if (foodItemId.HasValue && foodItemId.Value > 0)
        {
            var existingItem = await dbContext.FoodItems
                .FirstOrDefaultAsync(x => x.Id == foodItemId.Value && x.PoiId == normalizedPoiId, cancellationToken);

            if (existingItem is null)
            {
                throw new InvalidOperationException("Food item not found for this POI.");
            }

            existingItem.Name = normalizedName;
            existingItem.Description = normalizedDescription;
            existingItem.Price = price;
            existingItem.Currency = normalizedCurrency;
            existingItem.IsAvailable = isAvailable;
            existingItem.DisplayOrder = displayOrder;
            existingItem.OwnerId = ownerUserId;
            existingItem.UpdatedUtc = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            return existingItem.Id;
        }

        var newFoodItem = new FoodItem
        {
            PoiId = normalizedPoiId,
            OwnerId = ownerUserId,
            Name = normalizedName,
            Description = normalizedDescription,
            Price = price,
            Currency = normalizedCurrency,
            IsAvailable = isAvailable,
            DisplayOrder = displayOrder,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        dbContext.FoodItems.Add(newFoodItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        return newFoodItem.Id;
    }

    public async Task<(bool IsSuccess, string Message)> DeleteOwnerFoodItemAsync(
        int ownerUserId,
        string poiId,
        int foodItemId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return (false, "POI id is required.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedPoiId = poiId.Trim().ToUpperInvariant();

        var hasPermission = await dbContext.Pois
            .AsNoTracking()
            .AnyAsync(x => x.Id == normalizedPoiId && x.OwnerId == ownerUserId, cancellationToken);

        if (!hasPermission)
        {
            return (false, "POI not found or not owned by this user.");
        }

        var existingItem = await dbContext.FoodItems
            .FirstOrDefaultAsync(x => x.Id == foodItemId && x.PoiId == normalizedPoiId, cancellationToken);

        if (existingItem is null)
        {
            return (false, "Food item not found for this POI.");
        }

        dbContext.FoodItems.Remove(existingItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, "Food item was deleted.");
    }

    public async Task<IReadOnlyList<POI>> GetPendingPoisAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Pois
            .AsNoTracking()
            .Include(x => x.Owner)
            .Where(x => x.ApprovalStatus == PoiApprovalStatus.Pending)
            .OrderBy(x => x.SubmittedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool IsSuccess, string Message)> ApprovePoiAsync(
        string poiId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var poi = await dbContext.Pois.FirstOrDefaultAsync(x => x.Id == poiId, cancellationToken);
        if (poi is null)
        {
            return (false, "POI not found.");
        }

        poi.ApprovalStatus = PoiApprovalStatus.Approved;
        poi.ReviewedByAdminUserId = adminUserId;
        poi.ReviewedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, $"Approved POI {poi.Id}.");
    }

    public async Task<(bool IsSuccess, string Message)> RejectPoiAsync(
        string poiId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var poi = await dbContext.Pois.FirstOrDefaultAsync(x => x.Id == poiId, cancellationToken);
        if (poi is null)
        {
            return (false, "POI not found.");
        }

        poi.ApprovalStatus = PoiApprovalStatus.Rejected;
        poi.ReviewedByAdminUserId = adminUserId;
        poi.ReviewedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, $"Rejected POI {poi.Id}.");
    }

    private static async Task<string> GenerateNextPoiIdInternalAsync(
        AppDbContext dbContext,
        PoiType type,
        CancellationToken cancellationToken)
    {
        var prefix = type switch
        {
            PoiType.Food => "FD",
            PoiType.Visit => "VS",
            _ => "VS"
        };

        var existingIds = await dbContext.Pois
            .AsNoTracking()
            .Where(x => x.Id.StartsWith(prefix + "-"))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var maxSequence = 0;
        foreach (var id in existingIds)
        {
            var segments = id.Split('-', 2, StringSplitOptions.TrimEntries);
            if (segments.Length != 2)
            {
                continue;
            }

            if (int.TryParse(segments[1], out var sequence) && sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        var nextSequence = maxSequence + 1;
        return $"{prefix}-{nextSequence:000}";
    }

    private static async Task<string> GenerateNextOwnerCodeAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingCodes = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.OwnerIdentificationCode != null && x.OwnerIdentificationCode.StartsWith("OWN-"))
            .Select(x => x.OwnerIdentificationCode!)
            .ToListAsync(cancellationToken);

        var maxSequence = 0;
        foreach (var code in existingCodes)
        {
            var segments = code.Split('-', 2, StringSplitOptions.TrimEntries);
            if (segments.Length != 2)
            {
                continue;
            }

            if (int.TryParse(segments[1], out var sequence) && sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        return $"OWN-{(maxSequence + 1):0000}";
    }

    private static async Task<Language> GetOrCreateLanguageAsync(
        AppDbContext dbContext,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        var language = await dbContext.Languages
            .FirstOrDefaultAsync(x => x.LanguageCode == normalizedLanguageCode, cancellationToken);

        if (language is not null)
        {
            return language;
        }

        language = new Language
        {
            LanguageCode = normalizedLanguageCode,
            LanguageName = normalizedLanguageCode.ToUpperInvariant()
        };

        dbContext.Languages.Add(language);
        await dbContext.SaveChangesAsync(cancellationToken);
        return language;
    }

    private static async Task EnsureOwnerCanUseLanguageAsync(
        AppDbContext dbContext,
        int ownerUserId,
        string normalizedLanguageCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedLanguageCode) || string.Equals(normalizedLanguageCode, "vi", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var language = await dbContext.Languages
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LanguageCode == normalizedLanguageCode, cancellationToken);

        if (language is null)
        {
            throw new InvalidOperationException($"Language '{normalizedLanguageCode}' does not exist. Please request ownership first.");
        }

        var hasApprovedOwnership = await dbContext.LanguageOwnershipRequests
            .AsNoTracking()
            .AnyAsync(
                x => x.OwnerUserId == ownerUserId
                    && x.LanguageId == language.Id
                    && x.Status == LanguageOwnershipRequestStatus.Approved,
                cancellationToken);

        if (!hasApprovedOwnership)
        {
            throw new InvalidOperationException($"Language '{normalizedLanguageCode}' is not approved for this owner account.");
        }
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "vi";
        }

        var normalized = languageCode.Trim().ToLowerInvariant();

        if (normalized.StartsWith("en", StringComparison.Ordinal) || normalized.Contains("english", StringComparison.Ordinal))
        {
            return "en";
        }

        if (normalized.StartsWith("fr", StringComparison.Ordinal) || normalized.Contains("french", StringComparison.Ordinal))
        {
            return "fr";
        }

        if (normalized.StartsWith("vi", StringComparison.Ordinal) || normalized.Contains("viet", StringComparison.Ordinal))
        {
            return "vi";
        }

        return normalized;
    }
}
