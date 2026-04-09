using FOOD_MAP.ViewModels;
using FOOD_MAP.Shared.Models;

namespace FOOD_MAP.Services;

public interface IPoiRepository
{
    // Lấy danh sách POI đã gắn với bản dịch theo ngôn ngữ yêu cầu.
    Task<IReadOnlyList<PoiListItemViewModel>> GetPoiItemsAsync(string languageCode, CancellationToken cancellationToken = default);
}
