using FOOD_MAP.Shared.Models;
using FOOD_MAP.ViewModels;

namespace FOOD_MAP.Services;

public sealed class PoiScanResult
{
    public string PoiId { get; init; } = string.Empty;

    public PoiType PoiType { get; init; } = PoiType.Visit;

    public string LocationName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string RichContentHtml { get; init; } = string.Empty;

    public string TtsScript { get; init; } = string.Empty;

    public IReadOnlyList<FoodMenuItemViewModel> FoodItems { get; init; } = Array.Empty<FoodMenuItemViewModel>();
}
