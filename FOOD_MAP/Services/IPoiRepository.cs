using FOOD_MAP.ViewModels;
using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.Services;

public interface IPoiRepository
{
    // Lấy danh sách POI đã gắn với bản dịch theo ngôn ngữ yêu cầu.
    Task<IReadOnlyList<PoiListItemViewModel>> GetPoiItemsAsync(string languageCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FoodMenuItemViewModel>> GetFoodItemsByPoiIdAsync(string poiId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PoiAvailableLanguageOption>> GetAvailableLanguagesForPoiAsync(string poiId, CancellationToken cancellationToken = default);

    Task<PoiScanResult?> GetPoiScanResultAsync(string poiId, string languageCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PoiListItemViewModel>> GetTourPoiItemsAsync(string tourCode, string languageCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TourSummaryViewModel>> GetTourSummariesAsync(CancellationToken cancellationToken = default);

    Task<TourSummaryViewModel?> CreateTourAsync(
        string? requestedName,
        IReadOnlyList<string> poiIds,
        bool isPublic,
        CancellationToken cancellationToken = default);

    Task<TourSummaryViewModel?> UpdateTourAsync(
        string tourCode,
        string? requestedName,
        IReadOnlyList<string> poiIds,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteTourAsync(string tourCode, CancellationToken cancellationToken = default);
}
