using System.Net.Http.Json;
using FOOD_MAP.Shared.Contracts;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.Shared.Services;
using FOOD_MAP.ViewModels;

namespace FOOD_MAP.Services;

public sealed class ApiDataService : IDataService
{
    public Task SeedDataAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class ApiAuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IUserSessionService _userSessionService;
    private readonly IUserActivityRepository _userActivityRepository;

    public ApiAuthService(
        HttpClient httpClient,
        IUserSessionService userSessionService,
        IUserActivityRepository userActivityRepository)
    {
        _httpClient = httpClient;
        _userSessionService = userSessionService;
        _userActivityRepository = userActivityRepository;
    }

    public async Task<(bool IsSuccess, string Message)> LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/mobile/auth/login",
            new AuthLoginRequestDto(userName, password),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthLoginResponseDto>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            return (false, "Không nhận được phản hồi từ máy chủ.");
        }

        if (!payload.IsSuccess)
        {
            return (false, payload.Message);
        }

        if (!payload.UserId.HasValue)
        {
            return (false, "Phản hồi đăng nhập không hợp lệ.");
        }

        _userSessionService.SignIn(payload.UserId.Value, payload.DisplayName ?? "User", payload.Role ?? UserRole.User);
        return (true, payload.Message);
    }

    public async Task<(bool IsSuccess, string Message)> RegisterAsync(string userName, string displayName, string password, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/mobile/auth/register",
            new AuthRegisterRequestDto(userName, displayName, password),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiResultDto>(cancellationToken: cancellationToken);
        return payload is null
            ? (false, "Không nhận được phản hồi từ máy chủ.")
            : (payload.IsSuccess, payload.Message);
    }

    public async Task ContinueAsGuestAsync(CancellationToken cancellationToken = default)
    {
        await _userActivityRepository.ClearLocalCacheAsync(cancellationToken);
        _userSessionService.UseGuestMode();
    }
}

public sealed class ApiUserProfileService : IUserProfileService
{
    private readonly HttpClient _httpClient;

    public ApiUserProfileService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UserProfileSnapshot?> GetProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/profile/{userId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<UserProfileDto>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            return null;
        }

        return new UserProfileSnapshot(
            payload.UserId,
            payload.UserName,
            payload.DisplayName,
            payload.Role,
            payload.OwnerIdentificationCode);
    }

    public async Task<(bool IsSuccess, string Message)> UpdateDisplayNameAsync(int userId, string displayName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/mobile/profile/{userId}/display-name",
            new UpdateDisplayNameRequestDto(displayName),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiResultDto>(cancellationToken: cancellationToken);

        return payload is null
            ? (false, "Không nhận được phản hồi từ máy chủ.")
            : (payload.IsSuccess, payload.Message);
    }

    public async Task<(bool IsSuccess, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/mobile/profile/{userId}/password",
            new ChangePasswordRequestDto(currentPassword, newPassword),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiResultDto>(cancellationToken: cancellationToken);

        return payload is null
            ? (false, "Không nhận được phản hồi từ máy chủ.")
            : (payload.IsSuccess, payload.Message);
    }
}

public sealed class ApiPoiRepository : IPoiRepository
{
    private readonly HttpClient _httpClient;
    private readonly IUserSessionService _userSessionService;

    public ApiPoiRepository(HttpClient httpClient, IUserSessionService userSessionService)
    {
        _httpClient = httpClient;
        _userSessionService = userSessionService;
    }

    public async Task<IReadOnlyList<PoiListItemViewModel>> GetPoiItemsAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        var userIdSegment = _userSessionService.CurrentUserId.HasValue
            ? $"&userId={_userSessionService.CurrentUserId.Value}"
            : string.Empty;

        var response = await _httpClient.GetAsync(
            $"api/mobile/pois?languageCode={Uri.EscapeDataString(normalizedLanguageCode)}{userIdSegment}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<PoiListItemDto>>(cancellationToken: cancellationToken)
            ?? new List<PoiListItemDto>();

        return payload.Select(item => new PoiListItemViewModel(
                item.PoiId,
                item.PoiType,
                item.Latitude,
                item.Longitude,
                item.Name,
                item.DistanceText,
                item.Description,
                item.NarrationText,
                item.ImageUrl,
                item.RichContentHtml,
            activationRadius: item.ActivationRadius,
            priority: item.Priority))
            .ToList();
    }

    public async Task<IReadOnlyList<FoodMenuItemViewModel>> GetFoodItemsByPoiIdAsync(string poiId, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        if (string.IsNullOrWhiteSpace(normalizedPoiId))
        {
            return Array.Empty<FoodMenuItemViewModel>();
        }

        var response = await _httpClient.GetAsync($"api/mobile/pois/{Uri.EscapeDataString(normalizedPoiId)}/food-items", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<FoodMenuItemDto>>(cancellationToken: cancellationToken)
            ?? new List<FoodMenuItemDto>();

        return payload
            .Select(item => new FoodMenuItemViewModel(item.Id, item.Name, item.Description, item.Price, item.Currency, item.IsAvailable))
            .ToList();
    }

    public async Task<IReadOnlyList<PoiAvailableLanguageOption>> GetAvailableLanguagesForPoiAsync(string poiId, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        if (string.IsNullOrWhiteSpace(normalizedPoiId))
        {
            return Array.Empty<PoiAvailableLanguageOption>();
        }

        var response = await _httpClient.GetAsync($"api/mobile/pois/{Uri.EscapeDataString(normalizedPoiId)}/languages", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<PoiAvailableLanguageOptionDto>>(cancellationToken: cancellationToken)
            ?? new List<PoiAvailableLanguageOptionDto>();

        return payload
            .Select(item => new PoiAvailableLanguageOption
            {
                LanguageCode = item.LanguageCode,
                LanguageName = item.LanguageName
            })
            .ToList();
    }

    public async Task<PoiScanResult?> GetPoiScanResultAsync(string poiId, string languageCode, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        if (string.IsNullOrWhiteSpace(normalizedPoiId))
        {
            return null;
        }

        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        var userIdSegment = _userSessionService.CurrentUserId.HasValue
            ? $"&userId={_userSessionService.CurrentUserId.Value}"
            : string.Empty;

        var response = await _httpClient.GetAsync(
            $"api/mobile/pois/{Uri.EscapeDataString(normalizedPoiId)}/scan?languageCode={Uri.EscapeDataString(normalizedLanguageCode)}{userIdSegment}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PoiScanResultDto>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            return null;
        }

        return new PoiScanResult
        {
            PoiId = payload.PoiId,
            PoiType = payload.PoiType,
            LocationName = payload.LocationName,
            Description = payload.Description,
            RichContentHtml = payload.RichContentHtml,
            TtsScript = payload.TtsScript,
            FoodItems = payload.FoodItems
                .Select(item => new FoodMenuItemViewModel(item.Id, item.Name, item.Description, item.Price, item.Currency, item.IsAvailable))
                .ToList()
        };
    }

    private static string NormalizePoiId(string poiId)
    {
        return string.IsNullOrWhiteSpace(poiId)
            ? string.Empty
            : poiId.Trim().ToUpperInvariant();
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

        if (normalized.StartsWith("vi", StringComparison.Ordinal) || normalized.Contains("viet", StringComparison.Ordinal))
        {
            return "vi";
        }

        if (normalized.StartsWith("fr", StringComparison.Ordinal) || normalized.Contains("french", StringComparison.Ordinal))
        {
            return "fr";
        }

        return normalized;
    }
}

public sealed class ApiUserActivityRepository : IUserActivityRepository
{
    private readonly HttpClient _httpClient;

    public ApiUserActivityRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<HashSet<string>> GetFavoritePoiIdsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/users/{userId}/activity/favorites", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken: cancellationToken)
            ?? new List<string>();

        return payload.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<HashSet<string>> GetVisitedPoiIdsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/users/{userId}/activity/visited", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken: cancellationToken)
            ?? new List<string>();

        return payload.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<int> GetTourCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/users/{userId}/activity/tour-count", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);
        return payload;
    }

    public async Task<bool> SetFavoriteAsync(int userId, string poiId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/users/{userId}/activity/favorite",
            new SetFavoriteRequestDto(poiId, isFavorite),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
        return payload;
    }

    public async Task AddTourAsync(int userId, string poiId, string languageCode, string triggerType, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/users/{userId}/activity/tour",
            new AddTourRequestDto(poiId, languageCode, triggerType),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public Task<int> FlushPendingOperationsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public Task ClearLocalCacheAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class ApiPoiWorkflowRepository : IPoiWorkflowRepository
{
    private readonly HttpClient _httpClient;

    public ApiPoiWorkflowRepository(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GenerateNextPoiIdAsync(PoiType type, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/workflow/next-poi-id?type={type}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<string>(cancellationToken: cancellationToken) ?? string.Empty;
    }

    public async Task<IReadOnlyList<Language>> GetAvailableLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/mobile/workflow/languages", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<LanguageDto>>(cancellationToken: cancellationToken)
            ?? new List<LanguageDto>();

        return payload.Select(ToLanguageModel).ToList();
    }

    public async Task<int> SubmitLanguageOwnershipRequestAsync(int ownerUserId, string languageCode, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/mobile/workflow/language-ownership",
            new SubmitLanguageOwnershipRequestDto(ownerUserId, languageCode),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<LanguageOwnershipRequest>> GetOwnerLanguageOwnershipRequestsAsync(int ownerUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/workflow/language-ownership/owner/{ownerUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<LanguageOwnershipRequestDto>>(cancellationToken: cancellationToken)
            ?? new List<LanguageOwnershipRequestDto>();

        return payload.Select(ToLanguageOwnershipRequestModel).ToList();
    }

    public async Task<IReadOnlyList<LanguageOwnershipRequest>> GetPendingLanguageOwnershipRequestsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/mobile/workflow/language-ownership/pending", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<LanguageOwnershipRequestDto>>(cancellationToken: cancellationToken)
            ?? new List<LanguageOwnershipRequestDto>();

        return payload.Select(ToLanguageOwnershipRequestModel).ToList();
    }

    public async Task<(bool IsSuccess, string Message)> ApproveLanguageOwnershipRequestAsync(int requestId, int adminUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/workflow/language-ownership/{requestId}/approve",
            new ApproveRequestDto(adminUserId),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadApiResultAsync(response, cancellationToken);
    }

    public async Task<(bool IsSuccess, string Message)> RejectLanguageOwnershipRequestAsync(int requestId, int adminUserId, string rejectionReason, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/workflow/language-ownership/{requestId}/reject",
            new RejectRequestDto(adminUserId, rejectionReason),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadApiResultAsync(response, cancellationToken);
    }

    public async Task<int> SubmitOwnerRegistrationRequestAsync(int userId, string businessName, string businessAddress, string contactPhone, string? notes, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/mobile/workflow/owner-registration",
            new SubmitOwnerRegistrationRequestDto(userId, businessName, businessAddress, contactPhone, notes),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);
    }

    public async Task<OwnerRegistrationRequest?> GetLatestOwnerRegistrationRequestForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/workflow/owner-registration/latest/{userId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OwnerRegistrationRequestDto>(cancellationToken: cancellationToken);
        return payload is null ? null : ToOwnerRegistrationRequestModel(payload);
    }

    public async Task<(bool IsSuccess, string Message)> CancelPendingOwnerRegistrationRequestAsync(int userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/mobile/workflow/owner-registration/{userId}/cancel", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadApiResultAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<OwnerRegistrationRequest>> GetPendingOwnerRegistrationRequestsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/mobile/workflow/owner-registration/pending", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<OwnerRegistrationRequestDto>>(cancellationToken: cancellationToken)
            ?? new List<OwnerRegistrationRequestDto>();

        return payload.Select(ToOwnerRegistrationRequestModel).ToList();
    }

    public async Task<(bool IsSuccess, string Message)> ApproveOwnerRegistrationAsync(int requestId, int adminUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/workflow/owner-registration/{requestId}/approve",
            new ApproveRequestDto(adminUserId),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadApiResultAsync(response, cancellationToken);
    }

    public async Task<(bool IsSuccess, string Message)> RejectOwnerRegistrationAsync(int requestId, int adminUserId, string rejectionReason, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/workflow/owner-registration/{requestId}/reject",
            new RejectRequestDto(adminUserId, rejectionReason),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadApiResultAsync(response, cancellationToken);
    }

    public async Task<string> SubmitOwnerPoiAsync(int ownerUserId, PoiType type, double latitude, double longitude, int activationRadius, int priority, string? qrCodeId, string baseLanguageCode, string locationName, string description, string imageUrl, string audioFileUrl, string ttsScript, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/mobile/workflow/owner-poi",
            new SubmitOwnerPoiRequestDto(
                ownerUserId,
                type,
                latitude,
                longitude,
                activationRadius,
                priority,
                qrCodeId,
                baseLanguageCode,
                locationName,
                description,
                imageUrl,
                audioFileUrl,
                ttsScript),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<string>(cancellationToken: cancellationToken) ?? string.Empty;
    }

    public async Task<IReadOnlyList<POI>> GetOwnerPoisAsync(int ownerUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/workflow/owner-poi/{ownerUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<PoiDto>>(cancellationToken: cancellationToken)
            ?? new List<PoiDto>();

        return payload.Select(ToPoiModel).ToList();
    }

    public async Task<IReadOnlyList<PoiHeatmapBucketSummary>> GetOwnerPoiHeatmapSummaryAsync(int ownerUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/workflow/owner-heatmap/{ownerUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<OwnerPoiHeatmapDto>>(cancellationToken: cancellationToken)
            ?? new List<OwnerPoiHeatmapDto>();

        return payload
            .Select(item => new PoiHeatmapBucketSummary(
                item.PoiId,
                item.TourCount1Day,
                item.TourCount7Days,
                item.TourCount30Days))
            .ToList();
    }

    public async Task<(bool IsSuccess, string Message)> UpdateOwnerPoiBasicInfoAsync(int ownerUserId, string poiId, PoiType type, double latitude, double longitude, int activationRadius, int priority, string? qrCodeId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"api/mobile/workflow/owner-poi/{ownerUserId}/{Uri.EscapeDataString(poiId)}",
            new UpdateOwnerPoiBasicInfoRequestDto(type, latitude, longitude, activationRadius, priority, qrCodeId),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadApiResultAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<FoodItem>> GetOwnerFoodItemsAsync(int ownerUserId, string poiId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/mobile/workflow/owner-food/{ownerUserId}/{Uri.EscapeDataString(poiId)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<FoodItemDto>>(cancellationToken: cancellationToken)
            ?? new List<FoodItemDto>();

        return payload.Select(ToFoodItemModel).ToList();
    }

    public async Task<int> SaveOwnerFoodItemAsync(int ownerUserId, string poiId, int? foodItemId, string name, string? description, decimal price, string currency, bool isAvailable, int displayOrder, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/mobile/workflow/owner-food",
            new SaveOwnerFoodItemRequestDto(ownerUserId, poiId, foodItemId, name, description, price, currency, isAvailable, displayOrder),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);
    }

    public async Task<(bool IsSuccess, string Message)> DeleteOwnerFoodItemAsync(int ownerUserId, string poiId, int foodItemId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(
            $"api/mobile/workflow/owner-food/{ownerUserId}/{Uri.EscapeDataString(poiId)}/{foodItemId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadApiResultAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<POI>> GetPendingPoisAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/mobile/workflow/pois/pending", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<PoiDto>>(cancellationToken: cancellationToken)
            ?? new List<PoiDto>();

        return payload.Select(ToPoiModel).ToList();
    }

    public async Task<(bool IsSuccess, string Message)> ApprovePoiAsync(string poiId, int adminUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/workflow/pois/{Uri.EscapeDataString(poiId)}/approve",
            new ApproveRequestDto(adminUserId),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadApiResultAsync(response, cancellationToken);
    }

    public async Task<(bool IsSuccess, string Message)> RejectPoiAsync(string poiId, int adminUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/workflow/pois/{Uri.EscapeDataString(poiId)}/reject",
            new ApproveRequestDto(adminUserId),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadApiResultAsync(response, cancellationToken);
    }

    private static async Task<(bool IsSuccess, string Message)> ReadApiResultAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var payload = await response.Content.ReadFromJsonAsync<ApiResultDto>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            return (false, "Không nhận được phản hồi từ máy chủ.");
        }

        return (payload.IsSuccess, payload.Message);
    }

    private static Language ToLanguageModel(LanguageDto dto)
    {
        return new Language
        {
            Id = dto.Id,
            LanguageCode = dto.LanguageCode,
            LanguageName = dto.LanguageName
        };
    }

    private static User? ToUserModel(UserSummaryDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        return new User
        {
            Id = dto.Id,
            UserName = dto.UserName,
            DisplayName = dto.DisplayName
        };
    }

    private static POITranslation ToPoiTranslationModel(PoiTranslationDto dto)
    {
        return new POITranslation
        {
            Id = dto.Id,
            PoiId = dto.PoiId,
            LanguageId = dto.LanguageId,
            LocationName = dto.LocationName,
            Description = dto.Description,
            ImageUrl = dto.ImageUrl,
            AudioFileUrl = dto.AudioFileUrl,
            TtsScript = dto.TtsScript,
            RichContentHtml = dto.RichContentHtml,
            Language = new Language
            {
                Id = dto.LanguageId,
                LanguageCode = dto.LanguageCode,
                LanguageName = dto.LanguageName
            }
        };
    }

    private static POI ToPoiModel(PoiDto dto)
    {
        return new POI
        {
            Id = dto.Id,
            Type = dto.Type,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            ActivationRadius = dto.ActivationRadius,
            Priority = dto.Priority,
            ApprovalStatus = dto.ApprovalStatus,
            SubmittedUtc = dto.SubmittedUtc,
            ReviewedUtc = dto.ReviewedUtc,
            OwnerId = dto.OwnerId,
            ReviewedByAdminUserId = dto.ReviewedByAdminUserId,
            QRCodeId = dto.QRCodeId,
            PoiTranslations = dto.PoiTranslations.Select(ToPoiTranslationModel).ToList(),
            Owner = ToUserModel(dto.Owner),
            ReviewedByAdminUser = ToUserModel(dto.ReviewedByAdminUser)
        };
    }

    private static FoodItem ToFoodItemModel(FoodItemDto dto)
    {
        return new FoodItem
        {
            Id = dto.Id,
            PoiId = dto.PoiId,
            OwnerId = dto.OwnerId,
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Currency = dto.Currency,
            IsAvailable = dto.IsAvailable,
            DisplayOrder = dto.DisplayOrder,
            CreatedUtc = dto.CreatedUtc,
            UpdatedUtc = dto.UpdatedUtc,
            Owner = ToUserModel(dto.Owner)
        };
    }

    private static LanguageOwnershipRequest ToLanguageOwnershipRequestModel(LanguageOwnershipRequestDto dto)
    {
        return new LanguageOwnershipRequest
        {
            Id = dto.Id,
            OwnerUserId = dto.OwnerUserId,
            LanguageId = dto.LanguageId,
            Status = dto.Status,
            RequestedUtc = dto.RequestedUtc,
            ReviewedUtc = dto.ReviewedUtc,
            ReviewedByAdminUserId = dto.ReviewedByAdminUserId,
            RejectionReason = dto.RejectionReason,
            OwnerUser = ToUserModel(dto.OwnerUser),
            Language = dto.Language is null ? null : ToLanguageModel(dto.Language),
            ReviewedByAdminUser = ToUserModel(dto.ReviewedByAdminUser)
        };
    }

    private static OwnerRegistrationRequest ToOwnerRegistrationRequestModel(OwnerRegistrationRequestDto dto)
    {
        return new OwnerRegistrationRequest
        {
            Id = dto.Id,
            UserId = dto.UserId,
            BusinessName = dto.BusinessName,
            BusinessAddress = dto.BusinessAddress,
            ContactPhone = dto.ContactPhone,
            Notes = dto.Notes,
            Status = dto.Status,
            RequestedUtc = dto.RequestedUtc,
            ReviewedUtc = dto.ReviewedUtc,
            ReviewedByAdminUserId = dto.ReviewedByAdminUserId,
            RejectionReason = dto.RejectionReason,
            ApprovedOwnerCode = dto.ApprovedOwnerCode,
            User = ToUserModel(dto.User),
            ReviewedByAdminUser = ToUserModel(dto.ReviewedByAdminUser)
        };
    }
}

public sealed class ApiActiveUserTrackerService : IActiveUserTrackerService
{
    private readonly HttpClient _httpClient;

    public ApiActiveUserTrackerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task TrackWebHeartbeatAsync(int? userId, string sessionKey, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task TrackMobileHeartbeatAsync(int? userId, string sessionKey, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/mobile/active-users/heartbeat/mobile",
            new MobileHeartbeatRequestDto(userId, sessionKey),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<ActiveUserSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("api/mobile/active-users/summary", cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ActiveUserSummary>(cancellationToken: cancellationToken);
        return payload ?? new ActiveUserSummary(0, 0, 0, 0);
    }
}