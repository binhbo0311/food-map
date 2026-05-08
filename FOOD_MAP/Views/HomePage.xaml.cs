using System.Collections.ObjectModel;
using System.Windows.Input;
using FOOD_MAP.Services;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP;

public partial class HomePage : ContentPage
{
    private readonly MobilePoiCatalogService _catalogService;
    private readonly IPoiRepository _poiRepository;
    private readonly IUserSessionService _userSessionService;
    private readonly string _languageCode;
    private bool _isLoading;
    private HomePoiSnapshot? _latestSnapshot;

    public HomePage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _catalogService = services.GetRequiredService<MobilePoiCatalogService>();
        _poiRepository = services.GetRequiredService<IPoiRepository>();
        _userSessionService = services.GetRequiredService<IUserSessionService>();
        _languageCode = Preferences.Default.Get("selected_language", "vi");
        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadAsync(true));
    }

    public ObservableCollection<HomePoiSection> Sections { get; } = [];

    public ObservableCollection<TourSummaryViewModel> TourSummaries { get; } = [];

    public ICommand RefreshCommand { get; }

    public bool IsRefreshing { get; private set; }

    public string StatusMessage { get; private set; } = string.Empty;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync(false);
    }

    private async Task LoadAsync(bool isRefresh)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        if (isRefresh)
        {
            IsRefreshing = true;
            OnPropertyChanged(nameof(IsRefreshing));
        }

        try
        {
            var snapshot = await _catalogService.BuildSnapshotAsync(_languageCode);
            _latestSnapshot = snapshot;

            Sections.Clear();
            foreach (var section in snapshot.Sections)
            {
                Sections.Add(section);
            }

            var tours = await _poiRepository.GetTourSummariesAsync();
            TourSummaries.Clear();
            foreach (var tour in tours)
            {
                TourSummaries.Add(tour);
            }

            StatusMessage = snapshot.HasLocation
                ? "Đã tải Home theo vị trí hiện tại."
                : "Không lấy được vị trí hiện tại, Home vẫn hiển thị dữ liệu theo danh mục hiện có.";
            OnPropertyChanged(nameof(StatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể tải Home: {ex.Message}";
            OnPropertyChanged(nameof(StatusMessage));
        }
        finally
        {
            if (isRefresh)
            {
                IsRefreshing = false;
                OnPropertyChanged(nameof(IsRefreshing));
            }

            _isLoading = false;
        }
    }

    private async void OnCreateTourClicked(object? sender, EventArgs e)
    {
        if (_latestSnapshot is null)
        {
            StatusMessage = "Chưa có dữ liệu vị trí để tạo tour.";
            OnPropertyChanged(nameof(StatusMessage));
            return;
        }

        var autoPoiIds = BuildAutoTourPoiIds(_latestSnapshot);
        if (autoPoiIds.Count == 0)
        {
            StatusMessage = "Không đủ POI gần nhất để tạo tour.";
            OnPropertyChanged(nameof(StatusMessage));
            return;
        }

        var isPublicTour = _userSessionService.CurrentRole is UserRole.Owner or UserRole.Admin;

        try
        {
            var createdTour = await _poiRepository.CreateTourAsync("quick_tour", autoPoiIds, isPublicTour);
            if (createdTour is null)
            {
                StatusMessage = "Không thể tạo tour lúc này.";
                OnPropertyChanged(nameof(StatusMessage));
                return;
            }

            TourSummaries.Insert(0, createdTour);
            StatusMessage = isPublicTour
                ? "Đã tạo Public tour mới."
                : "Đã tạo Private tour cho tài khoản của bạn.";
            OnPropertyChanged(nameof(StatusMessage));

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync($"{nameof(TourPage)}?tourCode={Uri.EscapeDataString(createdTour.TourCode)}");
                return;
            }

            await Navigation.PushAsync(new TourPage());
        }
        catch
        {
            StatusMessage = "Không thể tạo tour lúc này.";
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    private async void OnTourSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TourSummaryViewModel selectedTour)
        {
            return;
        }

        try
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync($"{nameof(TourPage)}?tourCode={Uri.EscapeDataString(selectedTour.TourCode)}");
                return;
            }

            await Navigation.PushAsync(new TourPage());
        }
        finally
        {
            if (sender is CollectionView collectionView)
            {
                collectionView.SelectedItem = null;
            }
        }
    }

    private async void OnCreateCustomTourClicked(object? sender, EventArgs e)
    {
        try
        {
            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(nameof(TourBuilderPage));
                return;
            }

            await Navigation.PushAsync(new TourBuilderPage());
        }
        catch
        {
            StatusMessage = "Không thể mở màn hình tạo tour tùy chỉnh.";
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    private static List<string> BuildAutoTourPoiIds(HomePoiSnapshot snapshot)
    {
        var nearbyRows = snapshot.NearbyPoiItems
            .OrderBy(row => row.DistanceKm ?? double.MaxValue)
            .ThenByDescending(row => row.Item.Priority)
            .ToList();

        var visitRows = nearbyRows
            .Where(row => !row.IsFoodPoi)
            .Take(3)
            .Select(row => row.Item.PoiId)
            .ToList();

        var foodRow = nearbyRows
            .Where(row => row.IsFoodPoi)
            .Select(row => row.Item.PoiId)
            .FirstOrDefault();

        var selectedPoiIds = new List<string>();
        selectedPoiIds.AddRange(visitRows);

        if (!string.IsNullOrWhiteSpace(foodRow))
        {
            selectedPoiIds.Add(foodRow);
        }

        if (selectedPoiIds.Count < 4)
        {
            var fallbackRows = nearbyRows
                .Select(row => row.Item.PoiId)
                .Where(poiId => !selectedPoiIds.Contains(poiId, StringComparer.OrdinalIgnoreCase))
                .Take(4 - selectedPoiIds.Count)
                .ToList();

            selectedPoiIds.AddRange(fallbackRows);
        }

        return selectedPoiIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToList();
    }
}