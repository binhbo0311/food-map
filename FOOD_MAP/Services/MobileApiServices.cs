using System.Net.Http.Json;
using System.IO;
using System.Text.Json;
using FOOD_MAP.Shared.Contracts;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.Shared.Services;
using FOOD_MAP.ViewModels;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;

namespace FOOD_MAP.Services;

internal static class OfflineCacheKeys
{
    public static string PoiItems(string languageCode) => $"api_cache_poi_items::{languageCode}";

    public static string FoodItems(string poiId) => $"api_cache_food_items::{poiId}";

    public static string PoiLanguages(string poiId) => $"api_cache_poi_languages::{poiId}";

    public static string PoiScan(string poiId, string languageCode) => $"api_cache_poi_scan::{poiId}::{languageCode}";

    public static string TourPoiItems(string tourCode, string languageCode) => $"api_cache_tour_pois::{tourCode}::{languageCode}";

    public const string TourSummaries = "api_cache_tour_summaries";

    public static string UserFavorites(int userId) => $"api_cache_user_favorites::{userId}";

    public static string UserVisited(int userId) => $"api_cache_user_visited::{userId}";

    public static string UserTourCount(int userId) => $"api_cache_user_tour_count::{userId}";

    public const string UserActivityPendingQueue = "api_cache_user_activity_pending_queue";
}

internal static class OfflineCacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Save<TPayload>(string key, TPayload payload)
    {
        var envelope = new CacheEnvelope<TPayload>
        {
            SavedUtc = DateTimeOffset.UtcNow,
            Payload = payload
        };

        Preferences.Default.Set(key, JsonSerializer.Serialize(envelope, JsonOptions));
    }

    public static bool TryGet<TPayload>(string key, out TPayload? payload)
    {
        payload = default;
        var rawJson = Preferences.Default.Get(key, string.Empty);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return false;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<CacheEnvelope<TPayload>>(rawJson, JsonOptions);
            if (envelope is null)
            {
                return false;
            }

            payload = envelope.Payload;
            return true;
        }
        catch
        {
            Preferences.Default.Remove(key);
            return false;
        }
    }

    public static void Remove(string key)
    {
        Preferences.Default.Remove(key);
    }

    private sealed class CacheEnvelope<TPayload>
    {
        public DateTimeOffset SavedUtc { get; set; }

        public TPayload Payload { get; set; } = default!;
    }
}

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
        var cacheKey = OfflineCacheKeys.PoiItems(normalizedLanguageCode);
        var userIdSegment = _userSessionService.CurrentUserId.HasValue
            ? $"&userId={_userSessionService.CurrentUserId.Value}"
            : string.Empty;

        try
        {
            var response = await _httpClient.GetAsync(
                $"api/mobile/pois?languageCode={Uri.EscapeDataString(normalizedLanguageCode)}{userIdSegment}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<List<PoiListItemDto>>(cancellationToken: cancellationToken)
                ?? [];

            OfflineCacheStore.Save(cacheKey, payload);
            return MapPoiItems(payload);
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            if (OfflineCacheStore.TryGet<List<PoiListItemDto>>(cacheKey, out var cachedPayload) && cachedPayload is not null)
            {
                return MapPoiItems(cachedPayload);
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<FoodMenuItemViewModel>> GetFoodItemsByPoiIdAsync(string poiId, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        if (string.IsNullOrWhiteSpace(normalizedPoiId))
        {
            return Array.Empty<FoodMenuItemViewModel>();
        }

        var cacheKey = OfflineCacheKeys.FoodItems(normalizedPoiId);

        try
        {
            var response = await _httpClient.GetAsync($"api/mobile/pois/{Uri.EscapeDataString(normalizedPoiId)}/food-items", cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<List<FoodMenuItemDto>>(cancellationToken: cancellationToken)
                ?? [];

            OfflineCacheStore.Save(cacheKey, payload);

            return payload
                .Select(item => new FoodMenuItemViewModel(item.Id, item.Name, item.Description, item.Price, item.Currency, item.IsAvailable))
                .ToList();
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            if (OfflineCacheStore.TryGet<List<FoodMenuItemDto>>(cacheKey, out var cachedPayload) && cachedPayload is not null)
            {
                return cachedPayload
                    .Select(item => new FoodMenuItemViewModel(item.Id, item.Name, item.Description, item.Price, item.Currency, item.IsAvailable))
                    .ToList();
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<PoiAvailableLanguageOption>> GetAvailableLanguagesForPoiAsync(string poiId, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        if (string.IsNullOrWhiteSpace(normalizedPoiId))
        {
            return Array.Empty<PoiAvailableLanguageOption>();
        }

        var cacheKey = OfflineCacheKeys.PoiLanguages(normalizedPoiId);

        try
        {
            var response = await _httpClient.GetAsync($"api/mobile/pois/{Uri.EscapeDataString(normalizedPoiId)}/languages", cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<List<PoiAvailableLanguageOptionDto>>(cancellationToken: cancellationToken)
                ?? [];

            OfflineCacheStore.Save(cacheKey, payload);

            return payload
                .Select(item => new PoiAvailableLanguageOption
                {
                    LanguageCode = item.LanguageCode,
                    LanguageName = item.LanguageName
                })
                .ToList();
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            if (OfflineCacheStore.TryGet<List<PoiAvailableLanguageOptionDto>>(cacheKey, out var cachedPayload) && cachedPayload is not null)
            {
                return cachedPayload
                    .Select(item => new PoiAvailableLanguageOption
                    {
                        LanguageCode = item.LanguageCode,
                        LanguageName = item.LanguageName
                    })
                    .ToList();
            }

            throw;
        }
    }

    public async Task<PoiScanResult?> GetPoiScanResultAsync(string poiId, string languageCode, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        if (string.IsNullOrWhiteSpace(normalizedPoiId))
        {
            return null;
        }

        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        var cacheKey = OfflineCacheKeys.PoiScan(normalizedPoiId, normalizedLanguageCode);
        var userIdSegment = _userSessionService.CurrentUserId.HasValue
            ? $"&userId={_userSessionService.CurrentUserId.Value}"
            : string.Empty;

        try
        {
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

            OfflineCacheStore.Save(cacheKey, payload);
            return MapPoiScanResult(payload);
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            if (OfflineCacheStore.TryGet<PoiScanResultDto>(cacheKey, out var cachedPayload) && cachedPayload is not null)
            {
                return MapPoiScanResult(cachedPayload);
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<PoiListItemViewModel>> GetTourPoiItemsAsync(string tourCode, string languageCode, CancellationToken cancellationToken = default)
    {
        var normalizedTourCode = string.IsNullOrWhiteSpace(tourCode)
            ? "DEFAULT"
            : tourCode.Trim().ToUpperInvariant();

        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        var cacheKey = OfflineCacheKeys.TourPoiItems(normalizedTourCode, normalizedLanguageCode);
        var userIdSegment = _userSessionService.CurrentUserId.HasValue
            ? $"&userId={_userSessionService.CurrentUserId.Value}"
            : string.Empty;

        try
        {
            var response = await _httpClient.GetAsync(
                $"api/mobile/tours/{Uri.EscapeDataString(normalizedTourCode)}/pois?languageCode={Uri.EscapeDataString(normalizedLanguageCode)}{userIdSegment}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<List<PoiListItemDto>>(cancellationToken: cancellationToken)
                ?? [];

            OfflineCacheStore.Save(cacheKey, payload);
            return MapPoiItems(payload);
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            if (OfflineCacheStore.TryGet<List<PoiListItemDto>>(cacheKey, out var cachedPayload) && cachedPayload is not null)
            {
                return MapPoiItems(cachedPayload);
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<TourSummaryViewModel>> GetTourSummariesAsync(CancellationToken cancellationToken = default)
    {
        var userIdSegment = _userSessionService.CurrentUserId.HasValue
            ? $"?userId={_userSessionService.CurrentUserId.Value}"
            : string.Empty;

        try
        {
            var response = await _httpClient.GetAsync($"api/mobile/tours{userIdSegment}", cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<List<TourSummaryDto>>(cancellationToken: cancellationToken)
                ?? [];

            OfflineCacheStore.Save(OfflineCacheKeys.TourSummaries, payload);

            return payload
                .Select(item => new TourSummaryViewModel(item.TourCode, item.PoiCount, item.TourName, item.OwnerUserId, item.IsPublic))
                .ToList();
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            if (OfflineCacheStore.TryGet<List<TourSummaryDto>>(OfflineCacheKeys.TourSummaries, out var cachedPayload) && cachedPayload is not null)
            {
                return cachedPayload
                    .Select(item => new TourSummaryViewModel(item.TourCode, item.PoiCount, item.TourName, item.OwnerUserId, item.IsPublic))
                    .ToList();
            }

            throw;
        }
    }

    public async Task<TourSummaryViewModel?> CreateTourAsync(
        string? requestedName,
        IReadOnlyList<string> poiIds,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        if (poiIds.Count == 0)
        {
            return null;
        }

        var payload = new CreateTourRequestDto(
            _userSessionService.CurrentUserId,
            _userSessionService.CurrentRole,
            requestedName,
            isPublic,
            poiIds);

        var response = await _httpClient.PostAsJsonAsync("api/mobile/tours", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        var createdTour = await response.Content.ReadFromJsonAsync<TourSummaryDto>(cancellationToken: cancellationToken);
        if (createdTour is null)
        {
            return null;
        }

        OfflineCacheStore.Remove(OfflineCacheKeys.TourSummaries);
        return new TourSummaryViewModel(createdTour.TourCode, createdTour.PoiCount, createdTour.TourName, createdTour.OwnerUserId, createdTour.IsPublic);
    }

    public async Task<TourSummaryViewModel?> UpdateTourAsync(
        string tourCode,
        string? requestedName,
        IReadOnlyList<string> poiIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tourCode) || poiIds.Count == 0)
        {
            return null;
        }

        var payload = new UpdateTourRequestDto(
            _userSessionService.CurrentUserId,
            _userSessionService.CurrentRole,
            requestedName,
            poiIds);

        var response = await _httpClient.PutAsJsonAsync(
            $"api/mobile/tours/{Uri.EscapeDataString(tourCode.Trim().ToUpperInvariant())}",
            payload,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var updatedTour = await response.Content.ReadFromJsonAsync<TourSummaryDto>(cancellationToken: cancellationToken);
        OfflineCacheStore.Remove(OfflineCacheKeys.TourSummaries);
        return updatedTour is null
            ? null
            : new TourSummaryViewModel(updatedTour.TourCode, updatedTour.PoiCount, updatedTour.TourName, updatedTour.OwnerUserId, updatedTour.IsPublic);
    }

    public async Task<bool> DeleteTourAsync(string tourCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tourCode))
        {
            return false;
        }

        var normalizedCode = tourCode.Trim().ToUpperInvariant();
        var userIdSegment = _userSessionService.CurrentUserId.HasValue
            ? _userSessionService.CurrentUserId.Value.ToString()
            : string.Empty;

        var response = await _httpClient.DeleteAsync(
            $"api/mobile/tours/{Uri.EscapeDataString(normalizedCode)}?userId={Uri.EscapeDataString(userIdSegment)}&userRole={Uri.EscapeDataString(_userSessionService.CurrentRole.ToString())}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        OfflineCacheStore.Remove(OfflineCacheKeys.TourSummaries);
        return true;
    }

    private static IReadOnlyList<PoiListItemViewModel> MapPoiItems(IReadOnlyList<PoiListItemDto> payload)
    {
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

    private static PoiScanResult MapPoiScanResult(PoiScanResultDto payload)
    {
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

    private static bool CanUseOfflineFallback(Exception ex, CancellationToken cancellationToken)
    {
        if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return ex is HttpRequestException
            || ex is TaskCanceledException
            || ex is IOException;
    }
}

public sealed class ApiUserActivityRepository : IUserActivityRepository
{
    private readonly HttpClient _httpClient;
    private readonly IUserSessionService _userSessionService;
    private readonly SemaphoreSlim _pendingOperationLock = new(1, 1);
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly object _recentTourLock = new();
    private readonly Dictionary<string, DateTimeOffset> _recentTourExpiryByKey = new(StringComparer.Ordinal);
    private const string FavoriteOperationType = "favorite";
    private const string TourOperationType = "tour";
    private static readonly JsonSerializerOptions PendingOperationJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan TourDuplicateWindow = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan RouteArrivalDuplicateWindow = TimeSpan.FromMinutes(6);

    public ApiUserActivityRepository(HttpClient httpClient, IUserSessionService userSessionService)
    {
        _httpClient = httpClient;
        _userSessionService = userSessionService;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            _ = FlushPendingOperationsAsync(CancellationToken.None);
        }
    }

    public async Task<HashSet<string>> GetFavoritePoiIdsAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/mobile/users/{userId}/activity/favorites", cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken: cancellationToken)
                ?? [];

            var normalized = payload
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizePoiId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            OfflineCacheStore.Save(OfflineCacheKeys.UserFavorites(userId), normalized);
            return normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            if (OfflineCacheStore.TryGet<List<string>>(OfflineCacheKeys.UserFavorites(userId), out var cachedPayload) && cachedPayload is not null)
            {
                return cachedPayload.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            throw;
        }
    }

    public async Task<HashSet<string>> GetVisitedPoiIdsAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/mobile/users/{userId}/activity/visited", cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<List<string>>(cancellationToken: cancellationToken)
                ?? [];

            var normalized = payload
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizePoiId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            OfflineCacheStore.Save(OfflineCacheKeys.UserVisited(userId), normalized);
            return normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            if (OfflineCacheStore.TryGet<List<string>>(OfflineCacheKeys.UserVisited(userId), out var cachedPayload) && cachedPayload is not null)
            {
                return cachedPayload.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            throw;
        }
    }

    public async Task<int> GetTourCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/mobile/users/{userId}/activity/tour-count", cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);
            OfflineCacheStore.Save(OfflineCacheKeys.UserTourCount(userId), payload);
            return payload;
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            if (OfflineCacheStore.TryGet<int>(OfflineCacheKeys.UserTourCount(userId), out var cachedPayload))
            {
                return cachedPayload;
            }

            throw;
        }
    }

    public async Task<bool> SetFavoriteAsync(int userId, string poiId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        await ApplyFavoriteSnapshotAsync(userId, normalizedPoiId, isFavorite, cancellationToken);

        if (!CanAttemptNetworkSync())
        {
            await EnqueueFavoriteOperationAsync(userId, normalizedPoiId, isFavorite, cancellationToken);
            return isFavorite;
        }

        try
        {
            await SendFavoriteAsync(userId, normalizedPoiId, isFavorite, cancellationToken);
            _ = FlushPendingOperationsAsync(CancellationToken.None);
            return isFavorite;
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            await EnqueueFavoriteOperationAsync(userId, normalizedPoiId, isFavorite, cancellationToken);
            return isFavorite;
        }
    }

    public async Task AddTourAsync(int userId, string poiId, string languageCode, string triggerType, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "vi" : languageCode.Trim().ToLowerInvariant();
        var normalizedTriggerType = string.IsNullOrWhiteSpace(triggerType) ? "manual" : triggerType.Trim().ToLowerInvariant();
        if (IsDuplicateTourRequest(userId, normalizedPoiId, normalizedTriggerType))
        {
            return;
        }

        await ApplyVisitedSnapshotAsync(userId, normalizedPoiId, cancellationToken);

        if (!CanAttemptNetworkSync())
        {
            await EnqueueTourOperationAsync(userId, normalizedPoiId, normalizedLanguageCode, normalizedTriggerType, cancellationToken);
            return;
        }

        try
        {
            await SendTourAsync(userId, normalizedPoiId, normalizedLanguageCode, normalizedTriggerType, cancellationToken);
            _ = FlushPendingOperationsAsync(CancellationToken.None);
        }
        catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
        {
            await EnqueueTourOperationAsync(userId, normalizedPoiId, normalizedLanguageCode, normalizedTriggerType, cancellationToken);
        }
    }

    public async Task<int> FlushPendingOperationsAsync(CancellationToken cancellationToken = default)
    {
        if (!CanAttemptNetworkSync())
        {
            return 0;
        }

        await _flushLock.WaitAsync(cancellationToken);
        try
        {
            List<PendingSyncOperation> pendingOperations;
            await _pendingOperationLock.WaitAsync(cancellationToken);
            try
            {
                pendingOperations = await LoadPendingOperationsUnsafeAsync();
            }
            finally
            {
                _pendingOperationLock.Release();
            }

            if (pendingOperations.Count == 0)
            {
                return 0;
            }

            var now = DateTimeOffset.UtcNow;
            var remainingOperations = new List<PendingSyncOperation>();
            var flushedCount = 0;

            foreach (var operation in pendingOperations.OrderBy(x => x.CreatedUtc))
            {
                if (operation.NextAttemptUtc.HasValue && operation.NextAttemptUtc.Value > now)
                {
                    remainingOperations.Add(operation);
                    continue;
                }

                try
                {
                    if (string.Equals(operation.OperationType, FavoriteOperationType, StringComparison.Ordinal))
                    {
                        if (!operation.IsFavorite.HasValue)
                        {
                            continue;
                        }

                        await SendFavoriteAsync(operation.UserId, operation.PoiId, operation.IsFavorite.Value, cancellationToken);
                    }
                    else if (string.Equals(operation.OperationType, TourOperationType, StringComparison.Ordinal))
                    {
                        await SendTourAsync(
                            operation.UserId,
                            operation.PoiId,
                            operation.LanguageCode ?? "vi",
                            operation.TriggerType ?? "manual",
                            cancellationToken);
                    }

                    flushedCount += 1;
                }
                catch (Exception ex) when (CanUseOfflineFallback(ex, cancellationToken))
                {
                    operation.RetryCount += 1;
                    operation.NextAttemptUtc = now.AddSeconds(GetBackoffSeconds(operation.RetryCount));
                    operation.LastError = BuildErrorMessage(ex);
                    remainingOperations.Add(operation);
                }
            }

            var compressedRemaining = CompressPendingOperations(remainingOperations);
            await _pendingOperationLock.WaitAsync(cancellationToken);
            try
            {
                await SavePendingOperationsUnsafeAsync(compressedRemaining);
            }
            finally
            {
                _pendingOperationLock.Release();
            }

            return flushedCount;
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async Task ClearLocalCacheAsync(CancellationToken cancellationToken = default)
    {
        await _pendingOperationLock.WaitAsync(cancellationToken);
        try
        {
            Preferences.Default.Remove(OfflineCacheKeys.UserActivityPendingQueue);
        }
        finally
        {
            _pendingOperationLock.Release();
        }

        var currentUserId = _userSessionService.CurrentUserId;
        if (currentUserId.HasValue)
        {
            OfflineCacheStore.Remove(OfflineCacheKeys.UserFavorites(currentUserId.Value));
            OfflineCacheStore.Remove(OfflineCacheKeys.UserVisited(currentUserId.Value));
            OfflineCacheStore.Remove(OfflineCacheKeys.UserTourCount(currentUserId.Value));
        }

        lock (_recentTourLock)
        {
            _recentTourExpiryByKey.Clear();
        }
    }

    private async Task SendFavoriteAsync(int userId, string poiId, bool isFavorite, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/users/{userId}/activity/favorite",
            new SetFavoriteRequestDto(poiId, isFavorite),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }

    private async Task SendTourAsync(int userId, string poiId, string languageCode, string triggerType, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/mobile/users/{userId}/activity/tour",
            new AddTourRequestDto(poiId, languageCode, triggerType),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task ApplyFavoriteSnapshotAsync(int userId, string poiId, bool isFavorite, CancellationToken cancellationToken)
    {
        await _pendingOperationLock.WaitAsync(cancellationToken);
        try
        {
            var favorites = OfflineCacheStore.TryGet<List<string>>(OfflineCacheKeys.UserFavorites(userId), out var cachedFavorites)
                && cachedFavorites is not null
                ? cachedFavorites.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (isFavorite)
            {
                favorites.Add(poiId);
            }
            else
            {
                favorites.Remove(poiId);
            }

            OfflineCacheStore.Save(OfflineCacheKeys.UserFavorites(userId), favorites.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
        }
        finally
        {
            _pendingOperationLock.Release();
        }
    }

    private async Task ApplyVisitedSnapshotAsync(int userId, string poiId, CancellationToken cancellationToken)
    {
        await _pendingOperationLock.WaitAsync(cancellationToken);
        try
        {
            var visited = OfflineCacheStore.TryGet<List<string>>(OfflineCacheKeys.UserVisited(userId), out var cachedVisited)
                && cachedVisited is not null
                ? cachedVisited.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var isNewVisit = visited.Add(poiId);
            OfflineCacheStore.Save(OfflineCacheKeys.UserVisited(userId), visited.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());

            if (isNewVisit)
            {
                var currentCount = OfflineCacheStore.TryGet<int>(OfflineCacheKeys.UserTourCount(userId), out var cachedCount)
                    ? cachedCount
                    : 0;

                OfflineCacheStore.Save(OfflineCacheKeys.UserTourCount(userId), currentCount + 1);
            }
        }
        finally
        {
            _pendingOperationLock.Release();
        }
    }

    private async Task EnqueueFavoriteOperationAsync(int userId, string poiId, bool isFavorite, CancellationToken cancellationToken)
    {
        await _pendingOperationLock.WaitAsync(cancellationToken);
        try
        {
            var pendingOperations = await LoadPendingOperationsUnsafeAsync();

            pendingOperations.RemoveAll(x =>
                string.Equals(x.OperationType, FavoriteOperationType, StringComparison.Ordinal)
                && x.UserId == userId
                && x.PoiId == poiId);

            pendingOperations.Add(new PendingSyncOperation
            {
                OperationType = FavoriteOperationType,
                UserId = userId,
                PoiId = poiId,
                IsFavorite = isFavorite,
                CreatedUtc = DateTimeOffset.UtcNow,
                RetryCount = 0
            });

            await SavePendingOperationsUnsafeAsync(CompressPendingOperations(pendingOperations));
        }
        finally
        {
            _pendingOperationLock.Release();
        }
    }

    private async Task EnqueueTourOperationAsync(
        int userId,
        string poiId,
        string languageCode,
        string triggerType,
        CancellationToken cancellationToken)
    {
        await _pendingOperationLock.WaitAsync(cancellationToken);
        try
        {
            var pendingOperations = await LoadPendingOperationsUnsafeAsync();
            var now = DateTimeOffset.UtcNow;
            var duplicateWindow = ResolveTourDuplicateWindow(triggerType);

            var hasSameRecentQueuedTour = pendingOperations.Any(x =>
                string.Equals(x.OperationType, TourOperationType, StringComparison.Ordinal)
                && x.UserId == userId
                && x.PoiId == poiId
                && string.Equals(x.TriggerType, triggerType, StringComparison.OrdinalIgnoreCase)
                && (now - x.CreatedUtc) <= duplicateWindow);

            if (!hasSameRecentQueuedTour)
            {
                pendingOperations.Add(new PendingSyncOperation
                {
                    OperationType = TourOperationType,
                    UserId = userId,
                    PoiId = poiId,
                    LanguageCode = languageCode,
                    TriggerType = triggerType,
                    CreatedUtc = now,
                    RetryCount = 0
                });
            }

            await SavePendingOperationsUnsafeAsync(CompressPendingOperations(pendingOperations));
        }
        finally
        {
            _pendingOperationLock.Release();
        }
    }

    private bool IsDuplicateTourRequest(int userId, string poiId, string triggerType)
    {
        var normalizedTriggerType = string.IsNullOrWhiteSpace(triggerType) ? "manual" : triggerType;
        var key = $"{userId}:{poiId}:{normalizedTriggerType.ToLowerInvariant()}";
        var now = DateTimeOffset.UtcNow;
        var duplicateWindow = ResolveTourDuplicateWindow(normalizedTriggerType);
        var expiresUtc = now.Add(duplicateWindow);

        lock (_recentTourLock)
        {
            var staleKeys = _recentTourExpiryByKey
                .Where(x => x.Value < now)
                .Select(x => x.Key)
                .ToList();

            foreach (var staleKey in staleKeys)
            {
                _recentTourExpiryByKey.Remove(staleKey);
            }

            if (_recentTourExpiryByKey.TryGetValue(key, out var existingExpiresUtc) && existingExpiresUtc >= now)
            {
                return true;
            }

            _recentTourExpiryByKey[key] = expiresUtc;
            return false;
        }
    }

    private static bool CanAttemptNetworkSync()
    {
        try
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
        catch
        {
            return true;
        }
    }

    private async Task<List<PendingSyncOperation>> LoadPendingOperationsUnsafeAsync()
    {
        var rawJson = Preferences.Default.Get(OfflineCacheKeys.UserActivityPendingQueue, string.Empty);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<PendingSyncOperation>>(rawJson, PendingOperationJsonOptions);
            return items ?? [];
        }
        catch
        {
            Preferences.Default.Remove(OfflineCacheKeys.UserActivityPendingQueue);
            return [];
        }
    }

    private Task SavePendingOperationsUnsafeAsync(List<PendingSyncOperation> operations)
    {
        if (operations.Count == 0)
        {
            Preferences.Default.Remove(OfflineCacheKeys.UserActivityPendingQueue);
            return Task.CompletedTask;
        }

        var rawJson = JsonSerializer.Serialize(operations, PendingOperationJsonOptions);
        Preferences.Default.Set(OfflineCacheKeys.UserActivityPendingQueue, rawJson);
        return Task.CompletedTask;
    }

    private static List<PendingSyncOperation> CompressPendingOperations(IEnumerable<PendingSyncOperation> operations)
    {
        var orderedOperations = operations.OrderBy(x => x.CreatedUtc).ToList();
        var latestFavoriteByKey = new Dictionary<string, PendingSyncOperation>(StringComparer.Ordinal);
        var compressedTours = new List<PendingSyncOperation>();

        foreach (var operation in orderedOperations)
        {
            if (string.Equals(operation.OperationType, FavoriteOperationType, StringComparison.Ordinal))
            {
                var favoriteKey = $"{operation.UserId}:{operation.PoiId}";
                latestFavoriteByKey[favoriteKey] = operation;
                continue;
            }

            if (!string.Equals(operation.OperationType, TourOperationType, StringComparison.Ordinal))
            {
                continue;
            }

            var hasNearDuplicate = compressedTours.Any(x =>
                x.UserId == operation.UserId
                && x.PoiId == operation.PoiId
                && string.Equals(x.TriggerType, operation.TriggerType, StringComparison.OrdinalIgnoreCase)
                && Math.Abs((x.CreatedUtc - operation.CreatedUtc).TotalSeconds) <= ResolveTourDuplicateWindow(operation.TriggerType).TotalSeconds);

            if (!hasNearDuplicate)
            {
                compressedTours.Add(operation);
            }
        }

        var result = new List<PendingSyncOperation>();
        result.AddRange(latestFavoriteByKey.Values.OrderBy(x => x.CreatedUtc));
        result.AddRange(compressedTours.OrderBy(x => x.CreatedUtc));
        return result.OrderBy(x => x.CreatedUtc).ToList();
    }

    private static int GetBackoffSeconds(int retryCount)
    {
        var clampedRetry = Math.Clamp(retryCount, 1, 6);
        return (int)Math.Pow(2, clampedRetry);
    }

    private static TimeSpan ResolveTourDuplicateWindow(string? triggerType)
    {
        return string.Equals(triggerType, "route_arrival", StringComparison.OrdinalIgnoreCase)
            ? RouteArrivalDuplicateWindow
            : TourDuplicateWindow;
    }

    private static string BuildErrorMessage(Exception ex)
    {
        var rootMessage = ex.GetBaseException().Message;
        return string.IsNullOrWhiteSpace(rootMessage) ? ex.Message : rootMessage;
    }

    private static string NormalizePoiId(string poiId)
    {
        return string.IsNullOrWhiteSpace(poiId)
            ? string.Empty
            : poiId.Trim().ToUpperInvariant();
    }

    private static bool CanUseOfflineFallback(Exception ex, CancellationToken cancellationToken)
    {
        if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return ex is HttpRequestException
            || ex is TaskCanceledException
            || ex is IOException;
    }

    private sealed class PendingSyncOperation
    {
        public string OperationType { get; set; } = string.Empty;

        public int UserId { get; set; }

        public string PoiId { get; set; } = string.Empty;

        public bool? IsFavorite { get; set; }

        public string? LanguageCode { get; set; }

        public string? TriggerType { get; set; }

        public DateTimeOffset CreatedUtc { get; set; }

        public int RetryCount { get; set; }

        public DateTimeOffset? NextAttemptUtc { get; set; }

        public string? LastError { get; set; }
    }
}

public sealed class ApiSyncService : ISyncService
{
    private readonly IPoiRepository _poiRepository;
    private readonly IUserActivityRepository _userActivityRepository;

    public ApiSyncService(IPoiRepository poiRepository, IUserActivityRepository userActivityRepository)
    {
        _poiRepository = poiRepository;
        _userActivityRepository = userActivityRepository;
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        await _userActivityRepository.FlushPendingOperationsAsync(cancellationToken);
        await _poiRepository.GetPoiItemsAsync("vi", cancellationToken);
        await _poiRepository.GetPoiItemsAsync("en", cancellationToken);
        await _poiRepository.GetTourSummariesAsync(cancellationToken);
    }

    public Task<bool> HasOfflineDataAsync(CancellationToken cancellationToken = default)
    {
        var hasOfflinePoiData =
            OfflineCacheStore.TryGet<List<PoiListItemDto>>(OfflineCacheKeys.PoiItems("vi"), out var viPois)
            && viPois is { Count: > 0 };

        hasOfflinePoiData = hasOfflinePoiData
            || (OfflineCacheStore.TryGet<List<PoiListItemDto>>(OfflineCacheKeys.PoiItems("en"), out var enPois)
                && enPois is { Count: > 0 });

        return Task.FromResult(hasOfflinePoiData);
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
