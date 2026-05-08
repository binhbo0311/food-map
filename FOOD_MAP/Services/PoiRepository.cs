using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.Shared.Services;
using FOOD_MAP.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Services;

public sealed class PoiRepository : IPoiRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IUserSessionService _userSessionService;

    public PoiRepository(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ISubscriptionService subscriptionService,
        IUserSessionService userSessionService)
    {
        _dbContextFactory = dbContextFactory;
        _subscriptionService = subscriptionService;
        _userSessionService = userSessionService;
    }

    public async Task<IReadOnlyList<PoiListItemViewModel>> GetPoiItemsAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        var subscriptionPolicy = await _subscriptionService.GetCurrentPolicyAsync(_userSessionService.CurrentUserId, cancellationToken);

        try
        {
            var pois = await dbContext.Pois
                .AsNoTracking()
                .Where(x => x.ApprovalStatus == PoiApprovalStatus.Approved)
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);

            if (pois.Count == 0)
            {
                return Array.Empty<PoiListItemViewModel>();
            }

            var poiIds = pois.Select(x => x.Id).ToArray();

            var translations = await dbContext.PoiTranslations
                .AsNoTracking()
                .Include(x => x.Language)
                .Where(x => poiIds.Contains(x.PoiId))
                .ToListAsync(cancellationToken);

            var items = new List<PoiListItemViewModel>(pois.Count);

            foreach (var poi in pois)
            {
                var translation = translations.FirstOrDefault(x => x.PoiId == poi.Id && IsLanguageMatch(x.Language?.LanguageCode, normalizedLanguageCode))
                                   ?? translations.FirstOrDefault(x => x.PoiId == poi.Id && IsLanguageMatch(x.Language?.LanguageCode, "en"))
                                   ?? translations.FirstOrDefault(x => x.PoiId == poi.Id && IsLanguageMatch(x.Language?.LanguageCode, "vi"))
                                   ?? translations.FirstOrDefault(x => x.PoiId == poi.Id);

                var displayName = translation?.LocationName ?? $"POI #{poi.Id}";
                var description = translation?.Description ?? string.Empty;
                var narrationText = string.IsNullOrWhiteSpace(translation?.TtsScript)
                    ? description
                    : translation!.TtsScript;
                var distanceText = $"Activation radius: {poi.ActivationRadius}m";
                var richContentHtml = translation?.RichContentHtml ?? string.Empty;

                items.Add(new PoiListItemViewModel(
                    poi.Id,
                    poi.Type,
                    poi.Latitude,
                    poi.Longitude,
                    displayName,
                    distanceText,
                    description,
                    narrationText,
                    translation?.ImageUrl ?? string.Empty,
                    richContentHtml,
                    activationRadius: poi.ActivationRadius,
                    priority: poi.Priority));
            }

            if (subscriptionPolicy.MaxAccessiblePoiCount.HasValue)
            {
                items = items.Take(subscriptionPolicy.MaxAccessiblePoiCount.Value).ToList();
            }

            if (items.Count > 0)
            {
                items[0].IsNearest = true;
            }

            return items;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Khong nuot loi ket noi DB de man hinh chinh hien thong bao ro nguyen nhan.
            throw new InvalidOperationException(
                "Không thể tải POI từ PostgreSQL. Hãy kiểm tra POSTGRES_HOST/POSTGRES_PORT và kết nối mạng từ thiết bị.",
                ex);
        }
    }

    public async Task<IReadOnlyList<FoodMenuItemViewModel>> GetFoodItemsByPoiIdAsync(string poiId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return Array.Empty<FoodMenuItemViewModel>();
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var items = await dbContext.FoodItems
            .AsNoTracking()
            .Where(x => x.PoiId == poiId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => new FoodMenuItemViewModel(
                x.Id,
                x.Name,
                x.Description ?? string.Empty,
                x.Price,
                x.Currency,
                x.IsAvailable))
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<IReadOnlyList<PoiAvailableLanguageOption>> GetAvailableLanguagesForPoiAsync(string poiId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return Array.Empty<PoiAvailableLanguageOption>();
        }

        var normalizedPoiId = poiId.Trim().ToUpperInvariant();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var poiExists = await dbContext.Pois
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == normalizedPoiId && x.ApprovalStatus == PoiApprovalStatus.Approved,
                cancellationToken);

        if (!poiExists)
        {
            return Array.Empty<PoiAvailableLanguageOption>();
        }

        var translations = await dbContext.PoiTranslations
            .AsNoTracking()
            .Include(x => x.Language)
            .Where(x => x.PoiId == normalizedPoiId)
            .ToListAsync(cancellationToken);

        var languageOptions = translations
            .Where(x => !string.IsNullOrWhiteSpace(x.Language?.LanguageCode))
            .Select(x =>
            {
                var normalizedCode = NormalizeLanguageCode(x.Language!.LanguageCode);
                var languageName = string.IsNullOrWhiteSpace(x.Language.LanguageName)
                    ? normalizedCode.ToUpperInvariant()
                    : x.Language.LanguageName.Trim();

                return new PoiAvailableLanguageOption
                {
                    LanguageCode = normalizedCode,
                    LanguageName = languageName
                };
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.LanguageCode))
            .GroupBy(x => x.LanguageCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(x => GetLanguagePriority(x.LanguageCode))
            .ThenBy(x => x.LanguageName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return languageOptions;
    }

    private static int GetLanguagePriority(string languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        return normalized switch
        {
            "vi" => 0,
            "en" => 1,
            _ => 2
        };
    }

    public async Task<PoiScanResult?> GetPoiScanResultAsync(string poiId, string languageCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return null;
        }

        var normalizedPoiId = poiId.Trim().ToUpperInvariant();
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var poi = await dbContext.Pois
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == normalizedPoiId && x.ApprovalStatus == PoiApprovalStatus.Approved,
                cancellationToken);

        if (poi is null)
        {
            return null;
        }

        var subscriptionPolicy = await _subscriptionService.GetCurrentPolicyAsync(_userSessionService.CurrentUserId, cancellationToken);
        if (subscriptionPolicy.MaxAccessiblePoiCount.HasValue)
        {
            var accessiblePoiIds = await dbContext.Pois
                .AsNoTracking()
                .Where(x => x.ApprovalStatus == PoiApprovalStatus.Approved)
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .Take(subscriptionPolicy.MaxAccessiblePoiCount.Value)
                .ToListAsync(cancellationToken);

            if (!accessiblePoiIds.Contains(normalizedPoiId, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        var translations = await dbContext.PoiTranslations
            .AsNoTracking()
            .Include(x => x.Language)
            .Where(x => x.PoiId == poi.Id)
            .ToListAsync(cancellationToken);

        var translation = translations.FirstOrDefault(x => IsLanguageMatch(x.Language?.LanguageCode, normalizedLanguageCode))
                          ?? translations.FirstOrDefault(x => IsLanguageMatch(x.Language?.LanguageCode, "en"))
                          ?? translations.FirstOrDefault(x => IsLanguageMatch(x.Language?.LanguageCode, "vi"))
                          ?? translations.FirstOrDefault();

        if (translation is null)
        {
            return null;
        }

        var foodItems = poi.Type == PoiType.Food
            ? await GetFoodItemsByPoiIdAsync(poi.Id, cancellationToken)
            : Array.Empty<FoodMenuItemViewModel>();

        return new PoiScanResult
        {
            PoiId = poi.Id,
            PoiType = poi.Type,
            LocationName = translation.LocationName,
            Description = translation.Description,
            TtsScript = string.IsNullOrWhiteSpace(translation.TtsScript) ? translation.Description : translation.TtsScript,
            RichContentHtml = translation.RichContentHtml,
            FoodItems = foodItems
        };
    }

    public async Task<IReadOnlyList<PoiListItemViewModel>> GetTourPoiItemsAsync(string tourCode, string languageCode, CancellationToken cancellationToken = default)
    {
        var normalizedTourCode = string.IsNullOrWhiteSpace(tourCode)
            ? "DEFAULT"
            : tourCode.Trim().ToUpperInvariant();

        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var subscriptionPolicy = await _subscriptionService.GetCurrentPolicyAsync(_userSessionService.CurrentUserId, cancellationToken);

        var tourRows = await dbContext.TourLists
            .AsNoTracking()
            .Where(x => x.TourCode == normalizedTourCode)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (tourRows.Count == 0)
        {
            return Array.Empty<PoiListItemViewModel>();
        }

        var orderedPoiIds = tourRows.Select(x => x.PoiId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var pois = await dbContext.Pois
            .AsNoTracking()
            .Where(x => orderedPoiIds.Contains(x.Id) && x.ApprovalStatus == PoiApprovalStatus.Approved)
            .ToListAsync(cancellationToken);

        if (pois.Count == 0)
        {
            return Array.Empty<PoiListItemViewModel>();
        }

        var translations = await dbContext.PoiTranslations
            .AsNoTracking()
            .Include(x => x.Language)
            .Where(x => orderedPoiIds.Contains(x.PoiId))
            .ToListAsync(cancellationToken);

        var poiById = pois.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var items = new List<PoiListItemViewModel>();

        foreach (var poiId in orderedPoiIds)
        {
            if (!poiById.TryGetValue(poiId, out var poi))
            {
                continue;
            }

            var translation = translations.FirstOrDefault(x => x.PoiId == poi.Id && IsLanguageMatch(x.Language?.LanguageCode, normalizedLanguageCode))
                              ?? translations.FirstOrDefault(x => x.PoiId == poi.Id && IsLanguageMatch(x.Language?.LanguageCode, "en"))
                              ?? translations.FirstOrDefault(x => x.PoiId == poi.Id && IsLanguageMatch(x.Language?.LanguageCode, "vi"))
                              ?? translations.FirstOrDefault(x => x.PoiId == poi.Id);

            var displayName = translation?.LocationName ?? $"POI #{poi.Id}";
            var description = translation?.Description ?? string.Empty;
            var narrationText = string.IsNullOrWhiteSpace(translation?.TtsScript)
                ? description
                : translation!.TtsScript;

            items.Add(new PoiListItemViewModel(
                poi.Id,
                poi.Type,
                poi.Latitude,
                poi.Longitude,
                displayName,
                $"Activation radius: {poi.ActivationRadius}m",
                description,
                narrationText,
                translation?.ImageUrl ?? string.Empty,
                translation?.RichContentHtml ?? string.Empty,
                activationRadius: poi.ActivationRadius,
                priority: poi.Priority));
        }

        if (subscriptionPolicy.MaxAccessiblePoiCount.HasValue)
        {
            items = items.Take(subscriptionPolicy.MaxAccessiblePoiCount.Value).ToList();
        }

        return items;
    }

    public async Task<IReadOnlyList<TourSummaryViewModel>> GetTourSummariesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var currentUserId = _userSessionService.CurrentUserId;
        var tourRows = await dbContext.TourLists
            .AsNoTracking()
            .OrderBy(x => x.TourCode)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (tourRows.Count == 0)
        {
            return Array.Empty<TourSummaryViewModel>();
        }

        var scopedRows = tourRows.Where(row => IsRowVisibleToUser(row, currentUserId))
            .ToList();

        return scopedRows
            .GroupBy(x => x.TourCode, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var first = group.First();
                return new TourSummaryViewModel(
                    group.Key,
                    group.Count(),
                    first.TourName,
                    first.OwnerUserId,
                    ResolveIsPublic(first));
            })
            .OrderBy(x => x.TourCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

        var currentUserId = _userSessionService.CurrentUserId;
        var canPublishTour = _userSessionService.CurrentRole is UserRole.Owner or UserRole.Admin;
        var isPublicTour = isPublic && canPublishTour;
        var scopePrefix = isPublicTour ? "PUB" : $"USR_{currentUserId ?? 0}";

        var normalizedName = string.IsNullOrWhiteSpace(requestedName)
            ? "TOUR"
            : new string(requestedName.Trim().ToUpperInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            normalizedName = "TOUR";
        }

        var tourCode = $"{scopePrefix}_{normalizedName}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        var normalizedPoiIds = poiIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedPoiIds.Count == 0)
        {
            return null;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var approvedPoiIds = await dbContext.Pois
            .AsNoTracking()
            .Where(x => normalizedPoiIds.Contains(x.Id) && x.ApprovalStatus == PoiApprovalStatus.Approved)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var orderedValidPoiIds = normalizedPoiIds
            .Where(x => approvedPoiIds.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (orderedValidPoiIds.Count == 0)
        {
            return null;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var rows = orderedValidPoiIds
            .Select((poiId, index) => new TourList
            {
                TourCode = tourCode,
                TourName = normalizedName,
                OwnerUserId = currentUserId,
                IsPublic = isPublicTour,
                PoiId = poiId,
                SortOrder = index,
                CreatedUtc = nowUtc
            })
            .ToList();

        await dbContext.TourLists.AddRangeAsync(rows, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TourSummaryViewModel(tourCode, rows.Count, normalizedName, currentUserId, isPublicTour);
    }

    public async Task<TourSummaryViewModel?> UpdateTourAsync(
        string tourCode,
        string? requestedName,
        IReadOnlyList<string> poiIds,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(tourCode)
            ? string.Empty
            : tourCode.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedCode) || poiIds.Count == 0)
        {
            return null;
        }

        var currentUserId = _userSessionService.CurrentUserId;
        var currentRole = _userSessionService.CurrentRole;
        var normalizedPoiIds = poiIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedPoiIds.Count == 0)
        {
            return null;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await dbContext.TourLists
            .Where(x => x.TourCode == normalizedCode)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        if (!CanManageTour(rows[0], currentUserId, currentRole))
        {
            return null;
        }

        var approvedPoiIds = await dbContext.Pois
            .AsNoTracking()
            .Where(x => normalizedPoiIds.Contains(x.Id) && x.ApprovalStatus == PoiApprovalStatus.Approved)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var orderedValidPoiIds = normalizedPoiIds
            .Where(x => approvedPoiIds.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (orderedValidPoiIds.Count == 0)
        {
            return null;
        }

        var updatedName = string.IsNullOrWhiteSpace(requestedName)
            ? rows[0].TourName
            : new string(requestedName.Trim().ToUpperInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());

        if (string.IsNullOrWhiteSpace(updatedName))
        {
            updatedName = rows[0].TourName;
        }

        var updatedOwnerUserId = rows[0].OwnerUserId;
        var updatedIsPublic = ResolveIsPublic(rows[0]);

        dbContext.TourLists.RemoveRange(rows);

        var nowUtc = DateTimeOffset.UtcNow;
        var updatedRows = orderedValidPoiIds
            .Select((poiId, index) => new TourList
            {
                TourCode = normalizedCode,
                TourName = updatedName,
                OwnerUserId = updatedOwnerUserId,
                IsPublic = updatedIsPublic,
                PoiId = poiId,
                SortOrder = index,
                CreatedUtc = nowUtc
            })
            .ToList();

        await dbContext.TourLists.AddRangeAsync(updatedRows, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new TourSummaryViewModel(normalizedCode, updatedRows.Count, updatedName, updatedOwnerUserId, updatedIsPublic);
    }

    public async Task<bool> DeleteTourAsync(string tourCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(tourCode)
            ? string.Empty
            : tourCode.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return false;
        }

        var currentUserId = _userSessionService.CurrentUserId;
        var currentRole = _userSessionService.CurrentRole;
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.TourLists
            .Where(x => x.TourCode == normalizedCode)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return false;
        }

        if (!CanManageTour(rows[0], currentUserId, currentRole))
        {
            return false;
        }

        dbContext.TourLists.RemoveRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RecordListenAsync(string poiId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(poiId))
        {
            return;
        }

        var normalizedPoiId = poiId.Trim().ToUpperInvariant();

        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Pois
                .Where(x => x.Id == normalizedPoiId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ListenCount, x => x.ListenCount + 1), cancellationToken);
        }
        catch
        {
            // Bỏ qua lỗi kết nối db cục bộ vì đây chỉ là analytics.
        }
    }

    private static bool IsRowVisibleToUser(TourList row, int? currentUserId)
    {
        if (ResolveIsPublic(row))
        {
            return true;
        }

        var ownerUserId = ResolveOwnerUserId(row);
        return currentUserId.HasValue && ownerUserId.HasValue && ownerUserId.Value == currentUserId.Value;
    }

    private static bool CanManageTour(TourList row, int? currentUserId, UserRole role)
    {
        if (ResolveIsPublic(row))
        {
            return role is UserRole.Owner or UserRole.Admin;
        }

        var ownerUserId = ResolveOwnerUserId(row);
        return currentUserId.HasValue && ownerUserId.HasValue && ownerUserId.Value == currentUserId.Value;
    }

    private static int? ResolveOwnerUserId(TourList row)
    {
        if (row.OwnerUserId.HasValue)
        {
            return row.OwnerUserId;
        }

        var tourCode = row.TourCode;
        if (!tourCode.StartsWith("USR_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var segments = tourCode.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
        {
            return null;
        }

        return int.TryParse(segments[1], out var ownerId) ? ownerId : null;
    }

    private static bool ResolveIsPublic(TourList row)
    {
        return row.IsPublic || row.TourCode.StartsWith("PUB_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLanguageMatch(string? candidateLanguageCode, string expectedLanguageCode)
    {
        var normalizedCandidate = NormalizeLanguageCode(candidateLanguageCode);
        return string.Equals(normalizedCandidate, expectedLanguageCode, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return string.Empty;
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

        return normalized;
    }
}
