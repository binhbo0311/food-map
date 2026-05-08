using System.Collections.ObjectModel;
using FOOD_MAP.Services;
using FOOD_MAP.Shared.Models;
using FOOD_MAP.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FOOD_MAP;

[QueryProperty(nameof(InitialTourCode), "tourCode")]
public partial class TourPage : ContentPage
{
    private readonly IPoiRepository _poiRepository;
    private readonly INarrationService _narrationService;
    private readonly IUserSessionService _userSessionService;
    private readonly string _languageCode;
    private bool _isLoading;
    private bool _isPlaying;
    private string? _initialTourCode;

    public TourPage()
    {
        InitializeComponent();

        var services = IPlatformApplication.Current?.Services ?? throw new InvalidOperationException("Service provider is not available.");
        _poiRepository = services.GetRequiredService<IPoiRepository>();
        _narrationService = services.GetRequiredService<INarrationService>();
        _userSessionService = services.GetRequiredService<IUserSessionService>();
        _languageCode = Preferences.Default.Get("selected_language", "vi");

        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadToursAsync(true));
    }

    public ObservableCollection<TourSummaryViewModel> TourSummaries { get; } = [];

    public ObservableCollection<PoiListItemViewModel> TourPois { get; } = [];

    public Command RefreshCommand { get; }

    public bool IsRefreshing { get; private set; }

    public TourSummaryViewModel? SelectedTour { get; private set; }

    public PoiListItemViewModel? SelectedTourPoi { get; private set; }

    public bool HasSelectedTour => SelectedTour is not null && SelectedTourPoi is not null;

    public bool CanManageSelectedTour => SelectedTour is not null
        && SelectedTour.IsPublic
        && _userSessionService.CurrentRole is UserRole.Owner or UserRole.Admin;

    public string SelectedTourDisplayName => SelectedTour?.DisplayName ?? string.Empty;

    public string SelectedTourSummaryText => SelectedTour?.SummaryText ?? string.Empty;

    public string SelectedTourPoiImageUrl => SelectedTourPoi?.ImageUrl ?? string.Empty;

    public string SelectedTourPoiDescription => SelectedTourPoi?.Description ?? "Chọn một POI trong tour để xem mô tả.";

    public string StatusMessage { get; private set; } = string.Empty;

    public string? InitialTourCode
    {
        get => _initialTourCode;
        set => _initialTourCode = string.IsNullOrWhiteSpace(value)
            ? null
            : Uri.UnescapeDataString(value).Trim().ToUpperInvariant();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        SelectedTour = null;
        OnPropertyChanged(nameof(SelectedTour));
        await LoadToursAsync(false);
    }

    private async Task LoadToursAsync(bool isRefresh)
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
            var tourSummaries = await _poiRepository.GetTourSummariesAsync();

            TourSummaries.Clear();
            foreach (var item in tourSummaries)
            {
                TourSummaries.Add(item);
            }

            if (TourSummaries.Count > 0 && SelectedTour is null)
            {
                var preferredTour = !string.IsNullOrWhiteSpace(InitialTourCode)
                    ? TourSummaries.FirstOrDefault(x => string.Equals(x.TourCode, InitialTourCode, StringComparison.OrdinalIgnoreCase))
                    : null;

                await SelectTourAsync(preferredTour ?? TourSummaries[0]);
            }

            StatusMessage = TourSummaries.Count == 0
                ? "Không có dữ liệu TourList."
                : $"Đã tải {TourSummaries.Count} tour.";
            OnPropertyChanged(nameof(StatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể tải danh sách Tour: {ex.Message}";
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

    private async void OnTourSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TourSummaryViewModel tourSummary)
        {
            return;
        }

        await SelectTourAsync(tourSummary);
    }

    private async Task SelectTourAsync(TourSummaryViewModel tourSummary)
    {
        SelectedTour = tourSummary;
        OnPropertyChanged(nameof(SelectedTour));
        OnPropertyChanged(nameof(SelectedTourDisplayName));
        OnPropertyChanged(nameof(SelectedTourSummaryText));
        OnPropertyChanged(nameof(HasSelectedTour));
        OnPropertyChanged(nameof(CanManageSelectedTour));

        var tourPois = await _poiRepository.GetTourPoiItemsAsync(tourSummary.TourCode, _languageCode);

        TourPois.Clear();
        foreach (var poi in tourPois)
        {
            TourPois.Add(poi);
        }

        SelectedTourPoi = TourPois.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedTourPoi));
        OnPropertyChanged(nameof(SelectedTourPoiImageUrl));
        OnPropertyChanged(nameof(SelectedTourPoiDescription));
        OnPropertyChanged(nameof(HasSelectedTour));
        OnPropertyChanged(nameof(CanManageSelectedTour));

        StatusMessage = $"Đã mở {tourSummary.DisplayName} với {TourPois.Count} POI.";
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void OnTourPoiSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PoiListItemViewModel poiItem)
        {
            return;
        }

        SelectedTourPoi = poiItem;
        OnPropertyChanged(nameof(SelectedTourPoi));
        OnPropertyChanged(nameof(SelectedTourPoiImageUrl));
        OnPropertyChanged(nameof(SelectedTourPoiDescription));
        OnPropertyChanged(nameof(HasSelectedTour));
    }

    private async void OnPlayClicked(object? sender, EventArgs e)
    {
        if (_isPlaying || SelectedTourPoi is null)
        {
            return;
        }

        _isPlaying = true;
        try
        {
            var scanResult = await _poiRepository.GetPoiScanResultAsync(SelectedTourPoi.PoiId, _languageCode);
            if (scanResult is null)
            {
                StatusMessage = $"Không thể phát TTS cho {SelectedTourPoi.Name}.";
                OnPropertyChanged(nameof(StatusMessage));
                return;
            }

            await _narrationService.StopAsync();
            await _narrationService.PlayManualNarrationAsync(scanResult.TtsScript, _languageCode);

            StatusMessage = $"Đang phát TTS cho {scanResult.LocationName}.";
            OnPropertyChanged(nameof(StatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Không thể phát TTS: {ex.Message}";
            OnPropertyChanged(nameof(StatusMessage));
        }
        finally
        {
            _isPlaying = false;
        }
    }

    private async void OnEditTourClicked(object? sender, EventArgs e)
    {
        if (!CanManageSelectedTour || SelectedTour is null)
        {
            return;
        }

        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync($"{nameof(TourBuilderPage)}?tourCode={Uri.EscapeDataString(SelectedTour.TourCode)}");
            return;
        }

        await Navigation.PushAsync(new TourBuilderPage());
    }

    private async void OnDeleteTourClicked(object? sender, EventArgs e)
    {
        if (!CanManageSelectedTour || SelectedTour is null)
        {
            return;
        }

        var confirm = await DisplayAlertAsync("Tour", $"Xóa tour {SelectedTour.DisplayName}?", "Xóa", "Hủy");
        if (!confirm)
        {
            return;
        }

        var deleted = await _poiRepository.DeleteTourAsync(SelectedTour.TourCode);
        if (!deleted)
        {
            StatusMessage = "Không thể xóa tour này.";
            OnPropertyChanged(nameof(StatusMessage));
            return;
        }

        SelectedTour = null;
        SelectedTourPoi = null;
        TourPois.Clear();
        OnPropertyChanged(nameof(SelectedTour));
        OnPropertyChanged(nameof(SelectedTourPoi));
        OnPropertyChanged(nameof(SelectedTourDisplayName));
        OnPropertyChanged(nameof(SelectedTourSummaryText));
        OnPropertyChanged(nameof(SelectedTourPoiImageUrl));
        OnPropertyChanged(nameof(SelectedTourPoiDescription));
        OnPropertyChanged(nameof(HasSelectedTour));
        OnPropertyChanged(nameof(CanManageSelectedTour));

        await LoadToursAsync(false);
        StatusMessage = "Đã xóa tour public.";
        OnPropertyChanged(nameof(StatusMessage));
    }
}
