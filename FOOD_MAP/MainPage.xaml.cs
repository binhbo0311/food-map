using FOOD_MAP.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;

namespace FOOD_MAP;

public partial class MainPage : ContentPage
{
    private readonly IPoiRepository _poiRepository;
    private readonly string _languageCode;
    private bool _isLoaded;

    public MainPage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _poiRepository = services.GetRequiredService<IPoiRepository>();
        _languageCode = Preferences.Default.Get("selected_language", "vi");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isLoaded)
        {
            return;
        }

        await LoadMapPinsAsync();
        _isLoaded = true;
    }

    private async Task LoadMapPinsAsync()
    {
        var poiItems = await _poiRepository.GetPoiItemsAsync(_languageCode);

        map.Pins.Clear();
        foreach (var poi in poiItems)
        {
            map.Pins.Add(new Pin
            {
                Label = poi.Name,
                Address = poi.DistanceText,
                Location = new Location(poi.Latitude, poi.Longitude),
                Type = PinType.Place
            });
        }

        var firstPoi = poiItems.FirstOrDefault();
        if (firstPoi is null)
        {
            return;
        }

        map.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(firstPoi.Latitude, firstPoi.Longitude),
            Distance.FromMeters(500)));
    }
}
