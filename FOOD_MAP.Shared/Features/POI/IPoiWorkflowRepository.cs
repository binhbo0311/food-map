using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.Shared.Services;

public sealed record PoiHeatmapBucketSummary(
    string PoiId,
    int TourCount1Day,
    int TourCount7Days,
    int TourCount30Days);

public interface IPoiWorkflowRepository
{
    Task<string> GenerateNextPoiIdAsync(PoiType type, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Language>> GetAvailableLanguagesAsync(CancellationToken cancellationToken = default);

    Task<int> SubmitLanguageOwnershipRequestAsync(
        int ownerUserId,
        string languageCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LanguageOwnershipRequest>> GetOwnerLanguageOwnershipRequestsAsync(
        int ownerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LanguageOwnershipRequest>> GetPendingLanguageOwnershipRequestsAsync(CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> ApproveLanguageOwnershipRequestAsync(
        int requestId,
        int adminUserId,
        CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> RejectLanguageOwnershipRequestAsync(
        int requestId,
        int adminUserId,
        string rejectionReason,
        CancellationToken cancellationToken = default);

    Task<int> SubmitOwnerRegistrationRequestAsync(
        int userId,
        string businessName,
        string businessAddress,
        string contactPhone,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<OwnerRegistrationRequest?> GetLatestOwnerRegistrationRequestForUserAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> CancelPendingOwnerRegistrationRequestAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OwnerRegistrationRequest>> GetPendingOwnerRegistrationRequestsAsync(CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> ApproveOwnerRegistrationAsync(
        int requestId,
        int adminUserId,
        CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> RejectOwnerRegistrationAsync(
        int requestId,
        int adminUserId,
        string rejectionReason,
        CancellationToken cancellationToken = default);

    Task<string> SubmitOwnerPoiAsync(
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
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<POI>> GetOwnerPoisAsync(
        int ownerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PoiHeatmapBucketSummary>> GetOwnerPoiHeatmapSummaryAsync(
        int ownerUserId,
        CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> UpdateOwnerPoiBasicInfoAsync(
        int ownerUserId,
        string poiId,
        PoiType type,
        double latitude,
        double longitude,
        int activationRadius,
        int priority,
        string? qrCodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FoodItem>> GetOwnerFoodItemsAsync(
        int ownerUserId,
        string poiId,
        CancellationToken cancellationToken = default);

    Task<int> SaveOwnerFoodItemAsync(
        int ownerUserId,
        string poiId,
        int? foodItemId,
        string name,
        string? description,
        decimal price,
        string currency,
        bool isAvailable,
        int displayOrder,
        CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> DeleteOwnerFoodItemAsync(
        int ownerUserId,
        string poiId,
        int foodItemId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<POI>> GetPendingPoisAsync(CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> ApprovePoiAsync(
        string poiId,
        int adminUserId,
        CancellationToken cancellationToken = default);

    Task<(bool IsSuccess, string Message)> RejectPoiAsync(
        string poiId,
        int adminUserId,
        CancellationToken cancellationToken = default);
}
