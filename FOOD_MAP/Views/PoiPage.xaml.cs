using System.Collections.ObjectModel;
using FOOD_MAP.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP;

public partial class PoiPage : ContentPage
{
    private readonly MobilePoiCatalogService _catalogService;
    private readonly string _languageCode;

    public PoiPage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _catalogService = services.GetRequiredService<MobilePoiCatalogService>();
        _languageCode = Preferences.Default.Get("selected_language", "vi");
        BindingContext = this;
    }

    public ObservableCollection<PoiCatalogRow> PoiItems { get; } = [];

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var snapshot = await _catalogService.BuildSnapshotAsync(_languageCode);
            PoiItems.Clear();
            foreach (var row in snapshot.NearbyPoiItems)
            {
                PoiItems.Add(row);
            }
        }
        catch
        {
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await LoadAsync();
    }
}