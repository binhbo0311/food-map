using FOOD_MAP.Shared.Data;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FOOD_MAP.Services;

public sealed class PoiRepository : IPoiRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public PoiRepository(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<PoiListItemViewModel>> GetPoiItemsAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);

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
                    richContentHtml));
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
