using FOOD_MAP.Shared.Data;
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

                items.Add(new PoiListItemViewModel(
                    poi.Id,
                    poi.Latitude,
                    poi.Longitude,
                    displayName,
                    distanceText,
                    description,
                    narrationText,
                    translation?.ImageUrl ?? string.Empty));
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
