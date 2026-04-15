using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;
using System.Text.Json;

namespace FOOD_MAP.Services;

public sealed class UserActivityRepository : IUserActivityRepository
{
    private const string FavoriteOperationType = "favorite";
    private const string TourOperationType = "tour";
    private const string PendingOperationsPreferenceKey = "user_activity_pending_sync_v1";
    private static readonly TimeSpan TourDuplicateWindow = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan RouteArrivalDuplicateWindow = TimeSpan.FromMinutes(6);
    private static readonly JsonSerializerOptions PendingOperationJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly SemaphoreSlim _pendingOperationLock = new(1, 1);
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly object _recentTourLock = new();
    private readonly Dictionary<string, DateTimeOffset> _recentTourExpiryByKey = new(StringComparer.Ordinal);

    public UserActivityRepository(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;

        // Tự flush queue khi thiết bị có mạng trở lại để đồng bộ dữ liệu offline sớm nhất có thể.
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
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var favoritePoiIds = await dbContext.UserFavorites
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.PoiId)
            .ToListAsync(cancellationToken);

        return favoritePoiIds.ToHashSet();
    }

    public async Task<HashSet<string>> GetVisitedPoiIdsAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Lấy danh sách POI user đã nghe tour để khôi phục trạng thái sau đăng nhập.
        var visitedPoiIds = await dbContext.UserTours
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.PoiId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return visitedPoiIds.ToHashSet();
    }

    public async Task<int> GetTourCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.UserTours
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task<bool> SetFavoriteAsync(int userId, string poiId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        var syncType = isFavorite ? "FavoriteAdd" : "FavoriteRemove";

        try
        {
            if (!CanAttemptNetworkSync())
            {
                await EnqueueFavoriteOperationAsync(userId, normalizedPoiId, isFavorite, cancellationToken);
                await WriteSyncHistoryAsync(
                    syncType: syncType,
                    isSuccess: false,
                    errorMessage: "Queued: no network.",
                    cancellationToken: CancellationToken.None);

                return isFavorite;
            }

            await ExecuteWithRetryAsync(
                async token => await ApplyFavoriteStateAsync(userId, normalizedPoiId, isFavorite, token),
                cancellationToken);

            await WriteSyncHistoryAsync(
                syncType: syncType,
                isSuccess: true,
                errorMessage: null,
                cancellationToken: CancellationToken.None);

            _ = FlushPendingOperationsAsync(CancellationToken.None);
            return isFavorite;
        }
        catch (Exception ex)
        {
            await EnqueueFavoriteOperationAsync(userId, normalizedPoiId, isFavorite, cancellationToken);

            await WriteSyncHistoryAsync(
                syncType: syncType,
                isSuccess: false,
                errorMessage: $"Queued after failure: {BuildErrorMessage(ex)}",
                cancellationToken: CancellationToken.None);

            return isFavorite;
        }
    }

    public async Task AddTourAsync(int userId, string poiId, string languageCode, string triggerType, CancellationToken cancellationToken = default)
    {
        var normalizedPoiId = NormalizePoiId(poiId);
        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? "vi" : languageCode;
        var normalizedTriggerType = string.IsNullOrWhiteSpace(triggerType) ? "manual" : triggerType;
        if (IsDuplicateTourRequest(userId, normalizedPoiId, normalizedTriggerType))
        {
            return;
        }

        try
        {
            if (!CanAttemptNetworkSync())
            {
                await EnqueueTourOperationAsync(userId, normalizedPoiId, normalizedLanguageCode, normalizedTriggerType, cancellationToken);
                await WriteSyncHistoryAsync(
                    syncType: "TourAdd",
                    isSuccess: false,
                    errorMessage: "Queued: no network.",
                    cancellationToken: CancellationToken.None);

                return;
            }

            await ExecuteWithRetryAsync(
                async token => await AddTourRecordAsync(userId, normalizedPoiId, normalizedLanguageCode, normalizedTriggerType, token),
                cancellationToken);

            await WriteSyncHistoryAsync(
                syncType: "TourAdd",
                isSuccess: true,
                errorMessage: null,
                cancellationToken: CancellationToken.None);

            _ = FlushPendingOperationsAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            await EnqueueTourOperationAsync(userId, normalizedPoiId, normalizedLanguageCode, normalizedTriggerType, cancellationToken);

            await WriteSyncHistoryAsync(
                syncType: "TourAdd",
                isSuccess: false,
                errorMessage: $"Queued after failure: {BuildErrorMessage(ex)}",
                cancellationToken: CancellationToken.None);
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

                        await ApplyFavoriteStateAsync(operation.UserId, operation.PoiId, operation.IsFavorite.Value, cancellationToken);
                    }
                    else if (string.Equals(operation.OperationType, TourOperationType, StringComparison.Ordinal))
                    {
                        await AddTourRecordAsync(
                            operation.UserId,
                            operation.PoiId,
                            operation.LanguageCode ?? "vi",
                            operation.TriggerType ?? "manual",
                            cancellationToken);
                    }

                    flushedCount += 1;
                }
                catch (Exception ex)
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

            if (flushedCount > 0)
            {
                await WriteSyncHistoryAsync(
                    syncType: "PendingFlush",
                    isSuccess: true,
                    errorMessage: null,
                    cancellationToken: CancellationToken.None);
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
            // Xóa queue pending trong local storage để Guest mode không giữ dữ liệu cá nhân chưa đồng bộ.
            Preferences.Default.Remove(PendingOperationsPreferenceKey);
        }
        finally
        {
            _pendingOperationLock.Release();
        }

        lock (_recentTourLock)
        {
            // Xóa bộ nhớ chống duplicate tour đã ghi theo user để tránh rò rỉ trạng thái cá nhân.
            _recentTourExpiryByKey.Clear();
        }
    }

    private async Task ApplyFavoriteStateAsync(int userId, string poiId, bool isFavorite, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existingFavorite = await dbContext.UserFavorites
            .FirstOrDefaultAsync(x => x.UserId == userId && x.PoiId == poiId, cancellationToken);

        if (isFavorite && existingFavorite is null)
        {
            dbContext.UserFavorites.Add(new UserFavorite
            {
                UserId = userId,
                PoiId = poiId,
                CreatedUtc = DateTimeOffset.UtcNow
            });
        }

        if (!isFavorite && existingFavorite is not null)
        {
            dbContext.UserFavorites.Remove(existingFavorite);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AddTourRecordAsync(int userId, string poiId, string languageCode, string triggerType, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Trigger route_arrival dùng cửa sổ dài hơn để đảm bảo một lượt ghé chỉ cộng một lần.
        var duplicateWindow = ResolveTourDuplicateWindow(triggerType);
        var duplicateThresholdUtc = DateTimeOffset.UtcNow - duplicateWindow;
        var isDuplicateTour = await dbContext.UserTours
            .AsNoTracking()
            .AnyAsync(
                x => x.UserId == userId
                    && x.PoiId == poiId
                    && x.TriggerType == triggerType
                    && x.VisitedUtc >= duplicateThresholdUtc,
                cancellationToken);

        if (isDuplicateTour)
        {
            return;
        }

        dbContext.UserTours.Add(new UserTour
        {
            UserId = userId,
            PoiId = poiId,
            LanguageCode = languageCode,
            TriggerType = triggerType,
            VisitedUtc = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
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
                RetryCount = 0,
                NextAttemptUtc = null,
                LastError = null
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
                    RetryCount = 0,
                    NextAttemptUtc = null,
                    LastError = null
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
            // Lưu thời điểm hết hạn theo từng key để trigger window dài/ngắn không ảnh hưởng lẫn nhau.
            var staleKeys = _recentTourExpiryByKey
                .Where(x => x.Value < now)
                .Select(x => x.Key)
                .ToList();

            foreach (var staleKey in staleKeys)
            {
                _recentTourExpiryByKey.Remove(staleKey);
            }

            if (_recentTourExpiryByKey.TryGetValue(key, out var existingExpiresUtc)
                && existingExpiresUtc >= now)
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
            // Nếu không đọc được trạng thái mạng, vẫn thử sync để không bỏ lỡ trường hợp có kết nối.
            return true;
        }
    }

    private async Task<List<PendingSyncOperation>> LoadPendingOperationsUnsafeAsync()
    {
        var rawJson = Preferences.Default.Get(PendingOperationsPreferenceKey, string.Empty);
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
            // JSON queue hỏng thì xóa về trạng thái sạch để không khóa luồng sync.
            Preferences.Default.Remove(PendingOperationsPreferenceKey);
            return [];
        }
    }

    private Task SavePendingOperationsUnsafeAsync(List<PendingSyncOperation> operations)
    {
        if (operations.Count == 0)
        {
            Preferences.Default.Remove(PendingOperationsPreferenceKey);
            return Task.CompletedTask;
        }

        var rawJson = JsonSerializer.Serialize(operations, PendingOperationJsonOptions);
        Preferences.Default.Set(PendingOperationsPreferenceKey, rawJson);
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
                && Math.Abs((x.CreatedUtc - operation.CreatedUtc).TotalSeconds)
                    <= ResolveTourDuplicateWindow(operation.TriggerType).TotalSeconds);

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

    private static async Task ExecuteWithRetryAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        var delayScheduleMs = new[] { 300, 900, 1800 };

        for (var attempt = 0; attempt < delayScheduleMs.Length; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await operation(cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == delayScheduleMs.Length - 1)
                {
                    break;
                }

                await Task.Delay(delayScheduleMs[attempt], cancellationToken);
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }
    }

    private async Task WriteSyncHistoryAsync(string syncType, bool isSuccess, string? errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            dbContext.SyncHistories.Add(new SyncHistory
            {
                SyncType = syncType,
                StartedUtc = DateTimeOffset.UtcNow,
                CompletedUtc = DateTimeOffset.UtcNow,
                IsSuccess = isSuccess,
                ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                    ? null
                    : errorMessage[..Math.Min(errorMessage.Length, 1000)]
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Không chặn luồng chính nếu ghi log sync history thất bại.
        }
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
