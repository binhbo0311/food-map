using System.Collections.ObjectModel;
using FOOD_MAP.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP;

public partial class HomePage : ContentPage
{
    private readonly MobilePoiCatalogService _catalogService;
    private readonly string _languageCode;

    public HomePage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _catalogService = services.GetRequiredService<MobilePoiCatalogService>();
        _languageCode = Preferences.Default.Get("selected_language", "vi");
        BindingContext = this;
    }

    public ObservableCollection<PoiCatalogRow> FoodItems { get; } = [];

    public ObservableCollection<PoiCatalogRow> VisitItems { get; } = [];

    public string StatusMessage { get; private set; } = string.Empty;

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

            FoodItems.Clear();
            foreach (var row in snapshot.Sections.First(x => string.Equals(x.Title, "Food", StringComparison.OrdinalIgnoreCase)).Items)
            {
                FoodItems.Add(row);
            }

            VisitItems.Clear();
            foreach (var row in snapshot.Sections.First(x => string.Equals(x.Title, "Visit", StringComparison.OrdinalIgnoreCase)).Items)
            {
                VisitItems.Add(row);
            }

            StatusMessage = snapshot.HasLocation
                ? "Đã tải dữ liệu Home theo vị trí hiện tại."
                : "Không lấy được vị trí hiện tại, danh sách sẽ trống cho các mục ngoài 3km.";
            OnPropertyChanged(nameof(StatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể tải Home: {ex.Message}";
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await LoadAsync();
    }
}