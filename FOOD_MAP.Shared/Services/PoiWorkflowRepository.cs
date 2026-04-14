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

    public async Task<int> SubmitOwnerRegistrationRequestAsync(
        int userId,
        string businessName,
        string businessAddress,
        string contactPhone,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingPendingRequest = await dbContext.OwnerRegistrationRequests
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.Status == OwnerRegistrationStatus.Pending,
                cancellationToken);

        if (existingPendingRequest is not null)
        {
            // Khi user đang có pending request, chỉ cho phép chỉnh sửa chính request đang chờ duyệt.
            existingPendingRequest.BusinessName = businessName.Trim();
            existingPendingRequest.BusinessAddress = businessAddress.Trim();
            existingPendingRequest.ContactPhone = contactPhone.Trim();
            existingPendingRequest.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
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
            BusinessName = businessName.Trim(),
            BusinessAddress = businessAddress.Trim(),
            ContactPhone = contactPhone.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
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
        request.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason)
            ? "Rejected by administrator."
            : rejectionReason.Trim();

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
        var normalizedLanguageCode = NormalizeLanguageCode(baseLanguageCode);

        var language = await dbContext.Languages
            .FirstOrDefaultAsync(x => x.LanguageCode == normalizedLanguageCode, cancellationToken);

        if (language is null)
        {
            language = new Language
            {
                LanguageCode = normalizedLanguageCode,
                LanguageName = normalizedLanguageCode.ToUpperInvariant()
            };

            dbContext.Languages.Add(language);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var poi = new POI
        {
            Id = poiId,
            Type = type,
            Latitude = latitude,
            Longitude = longitude,
            ActivationRadius = activationRadius,
            Priority = priority,
            QRCodeId = string.IsNullOrWhiteSpace(qrCodeId) ? null : qrCodeId.Trim(),
            ApprovalStatus = PoiApprovalStatus.Pending,
            SubmittedUtc = DateTimeOffset.UtcNow,
            OwnerId = ownerUserId
        };

        dbContext.Pois.Add(poi);

        dbContext.PoiTranslations.Add(new POITranslation
        {
            PoiId = poi.Id,
            LanguageId = language.Id,
            LocationName = locationName.Trim(),
            Description = description.Trim(),
            ImageUrl = imageUrl.Trim(),
            AudioFileUrl = audioFileUrl.Trim(),
            TtsScript = ttsScript.Trim()
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
        poi.QRCodeId = string.IsNullOrWhiteSpace(qrCodeId) ? null : qrCodeId.Trim();

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

        if (string.IsNullOrWhiteSpace(name))
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

        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? "VND"
            : currency.Trim().ToUpperInvariant();

        if (foodItemId.HasValue && foodItemId.Value > 0)
        {
            var existingItem = await dbContext.FoodItems
                .FirstOrDefaultAsync(x => x.Id == foodItemId.Value && x.PoiId == normalizedPoiId, cancellationToken);

            if (existingItem is null)
            {
                throw new InvalidOperationException("Food item not found for this POI.");
            }

            existingItem.Name = name.Trim();
            existingItem.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
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
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
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
            PoiType.StayIn => "ST",
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
