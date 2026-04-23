using FOOD_MAP.Shared.Services;
using FOOD_MAP.ViewModels;
using Microsoft.Maui.Devices.Sensors;

namespace FOOD_MAP.Services;

public sealed record PoiCatalogRow(PoiListItemViewModel Item, double? DistanceKm)
{
    public string Title => Item.Name;

    public string Description => Item.Description;

    public string ImageUrl => Item.ImageUrl;

    public string DistanceLabel => DistanceKm.HasValue
        ? $"{DistanceKm.Value:0.0} km"
        : Item.DistanceText;

    public string PriorityLabel => $"P{Item.Priority}";

    public bool IsFavorite => Item.IsFavorite;

    public bool IsFoodPoi => Item.IsFoodPoi;
}

public sealed record HomePoiSection(string Title, IReadOnlyList<PoiCatalogRow> Items);

public sealed record HomePoiSnapshot(IReadOnlyList<HomePoiSection> Sections, IReadOnlyList<PoiCatalogRow> NearbyPoiItems, bool HasLocation);

public sealed class MobilePoiCatalogService
{
    private const double HomeRadiusKm = 3d;
    private readonly IPoiRepository _poiRepository;
    private readonly ILocationService _locationService;

    public MobilePoiCatalogService(IPoiRepository poiRepository, ILocationService locationService)
    {
        _poiRepository = poiRepository;
        _locationService = locationService;
    }

    public async Task<HomePoiSnapshot> BuildSnapshotAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        var poiItems = await _poiRepository.GetPoiItemsAsync(languageCode, cancellationToken);
        var currentLocation = await _locationService.GetLastKnownLocationAsync(cancellationToken);
        var hasLocation = currentLocation is not null;

        var catalogRows = poiItems
            .Select(item => new PoiCatalogRow(item, CalculateDistanceKm(currentLocation, item)))
            .ToList();

        var foodRows = BuildHomeRows(catalogRows, isFoodPoi: true);
        var visitRows = BuildHomeRows(catalogRows, isFoodPoi: false);
        var nearbyRows = catalogRows
            .OrderByDescending(row => row.IsFavorite)
            .ThenBy(row => row.DistanceKm ?? double.MaxValue)
            .ThenByDescending(row => row.Item.Priority)
            .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new HomePoiSnapshot(
            new[]
            {
                new HomePoiSection("Food", foodRows),
                new HomePoiSection("Visit", visitRows)
            },
            nearbyRows,
            hasLocation);
    }

    private static IReadOnlyList<PoiCatalogRow> BuildHomeRows(List<PoiCatalogRow> catalogRows, bool isFoodPoi)
    {
        return catalogRows
            .Where(row => row.IsFoodPoi == isFoodPoi)
            .Where(row => row.DistanceKm.HasValue && row.DistanceKm.Value <= HomeRadiusKm)
            .OrderByDescending(row => row.Item.Priority)
            .ThenBy(row => row.DistanceKm ?? double.MaxValue)
            .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private static double? CalculateDistanceKm(Location? currentLocation, PoiListItemViewModel item)
    {
        if (currentLocation is null)
        {
            return null;
        }

        var poiLocation = new Location(item.Latitude, item.Longitude);
        return Location.CalculateDistance(currentLocation, poiLocation, DistanceUnits.Kilometers);
    }
}