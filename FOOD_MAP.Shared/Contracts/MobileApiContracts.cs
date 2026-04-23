using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.Shared.Contracts;

public sealed record ApiResultDto(bool IsSuccess, string Message);

public sealed record AuthLoginRequestDto(string UserName, string Password);

public sealed record AuthLoginResponseDto(
    bool IsSuccess,
    string Message,
    int? UserId,
    string? DisplayName,
    UserRole? Role);

public sealed record AuthRegisterRequestDto(string UserName, string DisplayName, string Password);

public sealed record UserProfileDto(
    int UserId,
    string UserName,
    string DisplayName,
    UserRole Role,
    string? OwnerIdentificationCode);

public sealed record UpdateDisplayNameRequestDto(string DisplayName);

public sealed record ChangePasswordRequestDto(string CurrentPassword, string NewPassword);

public sealed record PoiListItemDto(
    string PoiId,
    PoiType PoiType,
    double Latitude,
    double Longitude,
    int ActivationRadius,
    int Priority,
    string Name,
    string DistanceText,
    string Description,
    string NarrationText,
    string ImageUrl,
    string RichContentHtml);

public sealed record FoodMenuItemDto(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    bool IsAvailable);

public sealed record PoiAvailableLanguageOptionDto(string LanguageCode, string LanguageName);

public sealed record PoiScanResultDto(
    string PoiId,
    PoiType PoiType,
    string LocationName,
    string Description,
    string RichContentHtml,
    string TtsScript,
    IReadOnlyList<FoodMenuItemDto> FoodItems);

public sealed record SetFavoriteRequestDto(string PoiId, bool IsFavorite);

public sealed record AddTourRequestDto(string PoiId, string LanguageCode, string TriggerType);

public sealed record OwnerPoiHeatmapDto(
    string PoiId,
    int TourCount1Day,
    int TourCount7Days,
    int TourCount30Days);

public sealed record LanguageDto(int Id, string LanguageCode, string LanguageName);

public sealed record UserSummaryDto(int Id, string UserName, string DisplayName);

public sealed record PoiTranslationDto(
    int Id,
    string PoiId,
    int LanguageId,
    string LanguageCode,
    string LanguageName,
    string LocationName,
    string Description,
    string ImageUrl,
    string AudioFileUrl,
    string TtsScript,
    string RichContentHtml);

public sealed record PoiDto(
    string Id,
    PoiType Type,
    double Latitude,
    double Longitude,
    int ActivationRadius,
    int Priority,
    PoiApprovalStatus ApprovalStatus,
    DateTimeOffset SubmittedUtc,
    DateTimeOffset? ReviewedUtc,
    int? OwnerId,
    int? ReviewedByAdminUserId,
    string? QRCodeId,
    IReadOnlyList<PoiTranslationDto> PoiTranslations,
    UserSummaryDto? Owner,
    UserSummaryDto? ReviewedByAdminUser);

public sealed record FoodItemDto(
    int Id,
    string PoiId,
    int? OwnerId,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    bool IsAvailable,
    int DisplayOrder,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    UserSummaryDto? Owner);

public sealed record LanguageOwnershipRequestDto(
    int Id,
    int OwnerUserId,
    int LanguageId,
    LanguageOwnershipRequestStatus Status,
    DateTimeOffset RequestedUtc,
    DateTimeOffset? ReviewedUtc,
    int? ReviewedByAdminUserId,
    string? RejectionReason,
    UserSummaryDto? OwnerUser,
    LanguageDto? Language,
    UserSummaryDto? ReviewedByAdminUser);

public sealed record OwnerRegistrationRequestDto(
    int Id,
    int UserId,
    string BusinessName,
    string BusinessAddress,
    string ContactPhone,
    string? Notes,
    OwnerRegistrationStatus Status,
    DateTimeOffset RequestedUtc,
    DateTimeOffset? ReviewedUtc,
    int? ReviewedByAdminUserId,
    string? RejectionReason,
    string? ApprovedOwnerCode,
    UserSummaryDto? User,
    UserSummaryDto? ReviewedByAdminUser);

public sealed record ApproveRequestDto(int AdminUserId);

public sealed record RejectRequestDto(int AdminUserId, string RejectionReason);

public sealed record SubmitLanguageOwnershipRequestDto(int OwnerUserId, string LanguageCode);

public sealed record SubmitOwnerRegistrationRequestDto(
    int UserId,
    string BusinessName,
    string BusinessAddress,
    string ContactPhone,
    string? Notes);

public sealed record SubmitOwnerPoiRequestDto(
    int OwnerUserId,
    PoiType Type,
    double Latitude,
    double Longitude,
    int ActivationRadius,
    int Priority,
    string? QrCodeId,
    string BaseLanguageCode,
    string LocationName,
    string Description,
    string ImageUrl,
    string AudioFileUrl,
    string TtsScript);

public sealed record UpdateOwnerPoiBasicInfoRequestDto(
    PoiType Type,
    double Latitude,
    double Longitude,
    int ActivationRadius,
    int Priority,
    string? QrCodeId);

public sealed record SaveOwnerFoodItemRequestDto(
    int OwnerUserId,
    string PoiId,
    int? FoodItemId,
    string Name,
    string? Description,
    decimal Price,
    string Currency,
    bool IsAvailable,
    int DisplayOrder);

public sealed record MobileHeartbeatRequestDto(int? UserId, string SessionKey);

public sealed record SubscriptionPlanDto(
    int Id,
    string PlanCode,
    string DisplayName,
    SubscriptionTier Tier,
    BillingPeriod BillingPeriod,
    decimal FixedPrice,
    string Currency,
    int? MaxAccessiblePoiCount,
    int? MaxOwnerPoiCount,
    int MaxActivationRadiusMeters,
    bool IsActive,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record CreateSubscriptionPlanRequestDto(
    int AdminUserId,
    string PlanCode,
    string DisplayName,
    SubscriptionTier Tier,
    BillingPeriod BillingPeriod,
    decimal FixedPrice,
    string Currency,
    int? MaxAccessiblePoiCount,
    int? MaxOwnerPoiCount,
    int MaxActivationRadiusMeters,
    bool IsActive);

public sealed record UpdateSubscriptionPlanRequestDto(
    int AdminUserId,
    string PlanCode,
    string DisplayName,
    SubscriptionTier Tier,
    BillingPeriod BillingPeriod,
    decimal FixedPrice,
    string Currency,
    int? MaxAccessiblePoiCount,
    int? MaxOwnerPoiCount,
    int MaxActivationRadiusMeters,
    bool IsActive);